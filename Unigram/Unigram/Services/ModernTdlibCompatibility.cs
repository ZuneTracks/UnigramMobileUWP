using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Logs;
using Unigram.Services.ViewService;
using Unigram.ViewModels;

namespace Unigram.Services
{
    public static class ModernTdlibCompatibility
    {
        public static MessageTopic GetMessageTopic(long threadId)
        {
            return threadId == 0 ? null : new MessageTopicThread(threadId);
        }

        public static Function SetMessageSenderBlocked(MessageSender sender, bool blocked)
        {
#if MODERN_TDLIB
            return new SetMessageSenderBlockList(sender, blocked ? new BlockListMain() : null);
#else
            return new ToggleMessageSenderIsBlocked(sender, blocked);
#endif
        }

        public static StickerType GetStickerType(bool masks)
        {
#if MODERN_TDLIB
            return masks ? new StickerTypeMask() : new StickerTypeRegular();
#else
            return null;
#endif
        }

        public static Function GetInstalledStickerSets(bool masks)
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.GetInstalledStickerSets(GetStickerType(masks));
#else
            return new Telegram.Td.Api.GetInstalledStickerSets(masks);
#endif
        }

        public static Function GetRecentStickers(bool masks)
        {
            return new Telegram.Td.Api.GetRecentStickers(masks);
        }

        public static Function GetArchivedStickerSets(bool masks, long offset, int limit)
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.GetArchivedStickerSets(GetStickerType(masks), offset, limit);
#else
            return new Telegram.Td.Api.GetArchivedStickerSets(masks, offset, limit);
#endif
        }

        public static Function ReorderInstalledStickerSets(bool masks, IList<long> stickerSetIds)
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.ReorderInstalledStickerSets(GetStickerType(masks), stickerSetIds);
#else
            return new Telegram.Td.Api.ReorderInstalledStickerSets(masks, stickerSetIds);
#endif
        }

        public static Function GetTrendingStickerSets()
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.GetTrendingStickerSets(new StickerTypeRegular(), 0, 24);
#else
            return new Telegram.Td.Api.GetTrendingStickerSets(0, 24);
#endif
        }

        public static Function GetStickers(string query, int limit)
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.GetStickers(new StickerTypeRegular(), query, limit, 0);
#else
            return new Telegram.Td.Api.GetStickers(query, limit);
#endif
        }

        public static Function SearchStickers(string query, int limit)
        {
#if MODERN_TDLIB
            return new Telegram.Td.Api.SearchStickers(new StickerTypeRegular(), string.Empty, query, new List<string>(), 0, limit);
#else
            return new Telegram.Td.Api.SearchStickers(query, limit);
#endif
        }

#if MODERN_TDLIB
        public static StickerSetInfo CreateStickerSetInfo(long id, string title, string name, Thumbnail thumbnail, Outline thumbnailOutline, bool isOwned, bool isInstalled, bool isArchived, bool isOfficial, bool isAnimated, bool isMasks, bool isViewed, int size, IList<Sticker> covers)
        {
            return new StickerSetInfo(id, title, name, thumbnail, thumbnailOutline, isOwned, isInstalled, isArchived, isOfficial, GetStickerType(isMasks), false, false, isViewed, size, covers);
        }
#else
        public static StickerSetInfo CreateStickerSetInfo(long id, string title, string name, Thumbnail thumbnail, IList<ClosedVectorPath> thumbnailOutline, bool isOwned, bool isInstalled, bool isArchived, bool isOfficial, bool isAnimated, bool isMasks, bool isViewed, int size, IList<Sticker> covers)
        {
            return new StickerSetInfo(id, title, name, thumbnail, thumbnailOutline, isInstalled, isArchived, isOfficial, isAnimated, isMasks, isViewed, size, covers);
        }
#endif

        public static bool IsStickerType(bool masks, StickerType stickerType)
        {
#if MODERN_TDLIB
            return masks ? stickerType is StickerTypeMask : stickerType is StickerTypeRegular;
#else
            return false;
#endif
        }

        public static ChatPermissions CreateChatPermissions(bool allowed)
        {
#if MODERN_TDLIB
            return new ChatPermissions(allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed);
#else
            return new ChatPermissions(allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed);
#endif
        }

        public static Message CreateMessage(long id, MessageSender sender, long chatId, MessageSendingState sendingState, MessageSchedulingState schedulingState, bool isOutgoing, bool isChannelPost, int date, MessageContent content)
        {
            return new Message(
                id,
                sender,
                null,
                chatId,
                sendingState,
                schedulingState,
                isOutgoing,
                false,
                false,
                false,
                false,
                isChannelPost,
                false,
                false,
                false,
                false,
                date,
                0,
                null,
                null,
                null,
                new List<UnreadReaction>(),
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                null,
                0,
                0,
                string.Empty,
                0,
                string.Empty,
                0,
                0,
                null,
                string.Empty,
                content,
                null,
                0);
        }
    }

    public interface IVoIPService : IHandle<UpdateCall>
    {
        string CurrentAudioInput { get; set; }
        float CurrentVolumeInput { get; set; }
        string CurrentAudioOutput { get; set; }
        float CurrentVolumeOutput { get; set; }
        Call ActiveCall { get; }
        void Show();
    }

    public sealed class VoIPService : TLViewModelBase, IVoIPService
    {
        public VoIPService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public string CurrentAudioInput { get; set; }
        public float CurrentVolumeInput { get; set; }
        public string CurrentAudioOutput { get; set; }
        public float CurrentVolumeOutput { get; set; }
        public Call ActiveCall => null;

        public void Show()
        {
            PushDiagnostics.Write("voip.disabled", "result=unsupported;feature=experimental_tdlib");
        }

        public void Handle(UpdateCall update)
        {
            PushDiagnostics.Write("voip.update", "result=ignored;feature=experimental_tdlib");
        }
    }
}

namespace Telegram.Td.Api
{
    using System.Collections.Generic;

    // The experimental app keeps the legacy filter view model for the main chat list,
    // while the folder editor is disabled until its ChatFolder port is complete.
    public sealed class ChatFilterInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string IconName { get; set; }
    }

    public sealed class ChatFilter
    {
        public string IconName { get; set; }
    }

    public sealed class RecommendedChatFilter
    {
    }

    public sealed class UpdateChatFilters
    {
        public IList<ChatFilterInfo> ChatFilters { get; set; } = new List<ChatFilterInfo>();
    }
}
