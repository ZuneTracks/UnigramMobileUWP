using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Storage;

namespace Unigram.Services
{
    // Copies small profile photos to LocalFolder\toast-avatars\{session}\{u|c}{id}.jpg so the
    // native push task (Unigram.Native.Tasks.NotificationTask::GetPicture) can show avatars
    // on toasts and the Live Tile while the app isn't running. Keys match the native push
    // group: "u" + user id, "c" + basic group / supergroup id. Secret chats are never cached.
    public class ToastAvatarCache
    {
        private const string FolderName = "toast-avatars";
        private const string LastSweepKey = "LastSweep";
        private const int SweepChatLimit = 50;
        private const int SweepDownloadLimit = 30;

        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan SweepDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

        private readonly IProtoService _protoService;
        private readonly int _session;
        private readonly object _fileLock = new object();
        private int _sweeping;

        // Set by Clear() on logout so in-flight sweeps or downloads can't recreate the cache.
        // Guarded by _fileLock, which also covers every file and settings write.
        private bool _closed;

        public ToastAvatarCache(IProtoService protoService, int session)
        {
            _protoService = protoService;
            _session = session;
        }

        private string Folder => Path.Combine(ApplicationData.Current.LocalFolder.Path, FolderName, _session.ToString());

        private ApplicationDataContainer Container => ApplicationData.Current.LocalSettings.CreateContainer($"{FolderName}-{_session}", ApplicationDataCreateDisposition.Always);

        public static string GetKey(Chat chat)
        {
            switch (chat?.Type)
            {
                case ChatTypePrivate privateChat:
                    return "u" + privateChat.UserId;
                case ChatTypeBasicGroup basicGroup:
                    return "c" + basicGroup.BasicGroupId;
                case ChatTypeSupergroup supergroup:
                    return "c" + supergroup.SupergroupId;
                default:
                    return null;
            }
        }

        public void ScheduleSweep()
        {
            lock (_fileLock)
            {
                _closed = false;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(SweepDelay);
                await SweepAsync();
            });
        }

        public async Task SweepAsync()
        {
            if (Interlocked.Exchange(ref _sweeping, 1) == 1)
            {
                return;
            }

            try
            {
                if (!IsSweepDue())
                {
                    return;
                }

                var chats = await _protoService.GetChatListAsync(new ChatListMain(), 0, SweepChatLimit);
                if (chats == null)
                {
                    Logs.PushDiagnostics.Write("toast.avatar.sweep", "result=no_chats");
                    return;
                }

                int cached = 0, unchanged = 0, downloaded = 0, removed = 0, failed = 0, skipped = 0, attempts = 0;

                foreach (var chatId in chats.ChatIds)
                {
                    if (IsClosed)
                    {
                        Logs.PushDiagnostics.Write("toast.avatar.sweep", "result=cancelled");
                        return;
                    }

                    var chat = _protoService.GetChat(chatId);
                    var key = GetKey(chat);
                    if (key == null)
                    {
                        skipped++;
                        continue;
                    }

                    var photo = chat.Photo?.Small;
                    if (photo == null)
                    {
                        if (Remove(key))
                        {
                            removed++;
                        }

                        continue;
                    }

                    if (IsCurrent(key, photo))
                    {
                        unchanged++;
                        continue;
                    }

                    if (!photo.Local.IsDownloadingCompleted)
                    {
                        if (attempts >= SweepDownloadLimit)
                        {
                            skipped++;
                            continue;
                        }

                        attempts++;
                        photo = await DownloadAsync(photo);
                        if (photo == null)
                        {
                            failed++;
                            continue;
                        }

                        downloaded++;
                    }

                    try
                    {
                        if (Store(key, photo))
                        {
                            cached++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (IOException)
                    {
                        failed++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        failed++;
                    }
                }

                lock (_fileLock)
                {
                    if (_closed)
                    {
                        return;
                    }

                    Container.Values[LastSweepKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }

                Logs.PushDiagnostics.Write("toast.avatar.sweep", $"result=done;chats={chats.ChatIds.Count};cached={cached};unchanged={unchanged};downloaded={downloaded};removed={removed};failed={failed};skipped={skipped}");
            }
            catch (Exception ex)
            {
                Logs.PushDiagnostics.WriteException("toast.avatar.sweep.failed", ex);
            }
            finally
            {
                Volatile.Write(ref _sweeping, 0);
            }
        }

        // Called when the app already has a downloaded photo for a chat or sender.
        public void Update(string key, Telegram.Td.Api.File photo)
        {
            if (key == null || photo == null || !photo.Local.IsDownloadingCompleted || IsCurrent(key, photo))
            {
                return;
            }

            try
            {
                Store(key, photo);
            }
            catch (Exception ex)
            {
                Logs.PushDiagnostics.WriteException("toast.avatar.update.failed", ex);
            }
        }

        public async Task UpdateAsync(Chat chat, ChatPhotoInfo photo)
        {
            var key = GetKey(chat);
            if (key == null)
            {
                return;
            }

            try
            {
                if (photo?.Small == null)
                {
                    Remove(key);
                    return;
                }

                var file = photo.Small;
                if (IsCurrent(key, file))
                {
                    return;
                }

                if (!file.Local.IsDownloadingCompleted)
                {
                    file = await DownloadAsync(file);
                }

                if (file != null)
                {
                    Store(key, file);
                }
            }
            catch (Exception ex)
            {
                Logs.PushDiagnostics.WriteException("toast.avatar.update.failed", ex);
            }
        }

        public void Clear()
        {
            try
            {
                lock (_fileLock)
                {
                    _closed = true;

                    if (Directory.Exists(Folder))
                    {
                        Directory.Delete(Folder, true);
                    }

                    ApplicationData.Current.LocalSettings.DeleteContainer($"{FolderName}-{_session}");
                }

                Logs.PushDiagnostics.Write("toast.avatar.clear", "result=success");
            }
            catch (Exception ex)
            {
                Logs.PushDiagnostics.WriteException("toast.avatar.clear.failed", ex);
            }
        }

        private bool IsClosed
        {
            get
            {
                lock (_fileLock)
                {
                    return _closed;
                }
            }
        }

        private bool IsSweepDue()
        {
            lock (_fileLock)
            {
                return !_closed
                    && !(Container.Values.TryGetValue(LastSweepKey, out object value) && value is long last
                        && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(last) < SweepInterval);
            }
        }

        private bool IsCurrent(string key, Telegram.Td.Api.File photo)
        {
            var uniqueId = photo.Remote?.UniqueId;
            if (string.IsNullOrEmpty(uniqueId))
            {
                return false;
            }

            lock (_fileLock)
            {
                // Reported as current once closed, so callers skip any further work.
                return _closed
                    || (Container.Values.TryGetValue(key, out object value)
                        && value is string stored
                        && stored == uniqueId
                        && System.IO.File.Exists(Path.Combine(Folder, key + ".jpg")));
            }
        }

        private async Task<Telegram.Td.Api.File> DownloadAsync(Telegram.Td.Api.File photo)
        {
            if (!photo.Local.CanBeDownloaded)
            {
                return null;
            }

            var download = _protoService.SendAsync(new DownloadFile(photo.Id, 1, 0, 0, true));
            var completed = await Task.WhenAny(download, Task.Delay(DownloadTimeout));
            if (completed == download && download.Status == TaskStatus.RanToCompletion
                && download.Result is Telegram.Td.Api.File file && file.Local.IsDownloadingCompleted)
            {
                return file;
            }

            return null;
        }

        private bool Store(string key, Telegram.Td.Api.File photo)
        {
            // Toast and tile templates on W10M reliably render JPEG; TDLib small chat photos are JPEG.
            var extension = Path.GetExtension(photo.Local.Path);
            if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            lock (_fileLock)
            {
                if (_closed)
                {
                    return false;
                }

                var folder = Folder;
                Directory.CreateDirectory(folder);

                // Copy to a temporary name first so the push task never reads a partial file.
                var target = Path.Combine(folder, key + ".jpg");
                var temp = target + ".tmp";
                System.IO.File.Copy(photo.Local.Path, temp, true);
                if (System.IO.File.Exists(target))
                {
                    System.IO.File.Delete(target);
                }

                System.IO.File.Move(temp, target);

                var uniqueId = photo.Remote?.UniqueId;
                if (!string.IsNullOrEmpty(uniqueId))
                {
                    Container.Values[key] = uniqueId;
                }
            }

            return true;
        }

        private bool Remove(string key)
        {
            lock (_fileLock)
            {
                if (_closed)
                {
                    return false;
                }

                var target = Path.Combine(Folder, key + ".jpg");
                Container.Values.Remove(key);

                if (System.IO.File.Exists(target))
                {
                    System.IO.File.Delete(target);
                    return true;
                }
            }

            return false;
        }
    }
}
