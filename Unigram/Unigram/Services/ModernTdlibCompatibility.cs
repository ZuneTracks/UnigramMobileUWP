using System.Collections.Generic;
using System.Linq;
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

        public static string GetWebPageTypeName(WebPage webPage)
        {
            return webPage?.Type;
        }

        public static TdlibParameters CreateTdlibParameters(bool useTestDc, string databaseDirectory, string filesDirectory, bool useFileDatabase, bool useChatInfoDatabase, bool useMessageDatabase, bool useSecretChats, int apiId, string apiHash, string systemLanguageCode, string deviceModel, string systemVersion, string applicationVersion, bool enableStorageOptimizer, bool ignoreFileNames)
        {
            return new TdlibParameters(useTestDc, databaseDirectory, filesDirectory, useFileDatabase, useChatInfoDatabase, useMessageDatabase, useSecretChats, apiId, apiHash, systemLanguageCode, deviceModel, systemVersion, applicationVersion, enableStorageOptimizer, ignoreFileNames);
        }

        public static Function CreateSetTdlibParameters(TdlibParameters parameters)
        {
            return new SetTdlibParameters(parameters);
        }

        public static Function CreateCheckDatabaseEncryptionKey(IList<byte> encryptionKey)
        {
            return new CheckDatabaseEncryptionKey(encryptionKey);
        }

        public static Function CreateGetWebPagePreview(FormattedText text)
        {
            return new GetWebPagePreview(text);
        }

        public static InputMessageContent CreateInputMessageText(FormattedText text, bool disableWebPagePreview, bool clearDraft)
        {
            return new InputMessageText(text, disableWebPagePreview, clearDraft);
        }

        public static Function CreateAddLocalMessage(long chatId, MessageSender senderId, long replyToMessageId, bool disableNotification, InputMessageContent inputMessageContent)
        {
            return new AddLocalMessage(chatId, senderId, replyToMessageId, disableNotification, inputMessageContent);
        }

        public static Function CreateSetLogStream(string path, int maxFileSize, bool redirectStderr)
        {
            return new SetLogStream(new LogStreamFile(path, maxFileSize, redirectStderr));
        }

        public static Function SetMessageSenderBlocked(MessageSender sender, bool blocked)
        {
#if MODERN_TDLIB
            return new SetMessageSenderBlockList(sender, blocked ? new BlockListMain() : null);
#else
            return new ToggleMessageSenderIsBlocked(sender, blocked);
#endif
        }

        public static MessageSendOptions CreateMessageSendOptions(bool disableNotification, bool fromBackground, MessageSchedulingState schedulingState)
        {
            return new MessageSendOptions(null, disableNotification, fromBackground, false, false, 0, false, schedulingState, 0, 0, false);
        }

        public static StickerType GetStickerType(bool masks)
        {
#if MODERN_TDLIB
            return masks ? (StickerType)new StickerTypeMask() : new StickerTypeRegular();
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

        public static bool GetAdministratorCanChangeInfo(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanChangeInfo == true;
#else
            return administrator.CanChangeInfo;
#endif
        }

        public static InputMessageContent CreateInputMessageDocument(InputFile file, InputThumbnail thumbnail, bool disableContentTypeDetection, FormattedText caption)
        {
#if MODERN_TDLIB
            return new InputMessageDocument(new InputDocument(file, thumbnail, disableContentTypeDetection), caption);
#else
            return new InputMessageDocument(file, thumbnail, disableContentTypeDetection, caption);
#endif
        }

        public static InputMessageContent CreateInputMessagePhoto(InputFile file, InputThumbnail thumbnail, int width, int height, FormattedText caption, int ttl)
        {
#if MODERN_TDLIB
            var photo = new InputPhoto(file, thumbnail, null, new int[0], width, height);
            return new InputMessagePhoto(photo, caption, false, ttl > 0 ? new MessageSelfDestructTypeTimer(ttl) : null, false);
#else
            return new InputMessagePhoto(file, thumbnail, new int[0], width, height, caption, ttl);
#endif
        }

        public static InputMessageContent CreateInputMessageAnimation(InputFile file, InputThumbnail thumbnail, int duration, int width, int height, FormattedText caption)
        {
#if MODERN_TDLIB
            var animation = new InputAnimation(file, thumbnail, new int[0], duration, width, height);
            return new InputMessageAnimation(animation, caption, false, false);
#else
            return new InputMessageAnimation(file, thumbnail, new int[0], duration, width, height, caption);
#endif
        }

        public static InputMessageContent CreateInputMessageVideo(InputFile file, InputThumbnail thumbnail, int duration, int width, int height, FormattedText caption, int ttl)
        {
#if MODERN_TDLIB
            var video = new InputVideo(file, thumbnail, null, 0, new int[0], duration, width, height, true);
            return new InputMessageVideo(video, caption, false, ttl > 0 ? new MessageSelfDestructTypeTimer(ttl) : null, false);
#else
            return new InputMessageVideo(file, thumbnail, new int[0], duration, width, height, true, caption, ttl);
#endif
        }

        public static InputMessageContent CreateInputMessageVideoNote(InputFile file, InputThumbnail thumbnail, int duration, int length)
        {
#if MODERN_TDLIB
            return new InputMessageVideoNote(new InputVideoNote(file, thumbnail, duration, length), null);
#else
            return new InputMessageVideoNote(file, thumbnail, duration, length);
#endif
        }

        public static InputMessageContent CreateInputMessageSticker(InputFile file, InputThumbnail thumbnail, int width, int height, string emoji)
        {
#if MODERN_TDLIB
            return new InputMessageSticker(new InputSticker(file, thumbnail, width, height), emoji);
#else
            return new InputMessageSticker(file, thumbnail, width, height, emoji);
#endif
        }

        public static InputMessageContent CreateInputMessageAudio(InputFile file, InputThumbnail thumbnail, int duration, string title, string performer, FormattedText caption)
        {
#if MODERN_TDLIB
            return new InputMessageAudio(new InputAudio(file, thumbnail, duration, title, performer), caption);
#else
            return new InputMessageAudio(file, thumbnail, duration, title, performer, caption);
#endif
        }

        public static InputMessageContent CreateInputMessageVoiceNote(InputFile file, int duration, FormattedText caption)
        {
#if MODERN_TDLIB
            return new InputMessageVoiceNote(new InputVoiceNote(file, duration, new byte[0]), caption, null);
#else
            return new InputMessageVoiceNote(file, duration, new byte[0], caption);
#endif
        }

        public static InputMessageContent CreateInputMessagePoll(string question, IList<string> options, bool isAnonymous, PollType type, bool allowsMultipleAnswers)
        {
#if MODERN_TDLIB
            var questionText = new FormattedText(question, new TextEntity[0]);
            var pollOptions = options.Select(option => new InputPollOption(new FormattedText(option, new TextEntity[0]), null)).ToList();
            InputPollType inputType;
            if (type is PollTypeQuiz quiz)
            {
                inputType = new InputPollTypeQuiz(new List<int>(quiz.CorrectOptionIds), quiz.Explanation, null);
            }
            else
            {
                inputType = new InputPollTypeRegular(allowsMultipleAnswers);
            }

            return new InputMessagePoll(questionText, pollOptions, null, null, isAnonymous, allowsMultipleAnswers, false, false, new List<string>(), false, false, inputType, 0, 0, false);
#else
            return new InputMessagePoll(question, options, isAnonymous, type, 0, 0, false);
#endif
        }

        public static PollType CreatePollTypeQuiz(int correctOptionId, FormattedText explanation)
        {
#if MODERN_TDLIB
            return new PollTypeQuiz(new List<int> { correctOptionId }, explanation, null);
#else
            return new PollTypeQuiz(correctOptionId, explanation);
#endif
        }

        public static PollType CreatePollTypeRegular(bool allowsMultipleAnswers)
        {
#if MODERN_TDLIB
            return new PollTypeRegular();
#else
            return new PollTypeRegular(allowsMultipleAnswers);
#endif
        }

        public static InputMessageContent CreateInputMessageLocation(Location location)
        {
#if MODERN_TDLIB
            return new InputMessageLocation(location);
#else
            return new InputMessageLocation(location, 0, 0, 0);
#endif
        }

        public static MessageContent CreateMessagePhoto(Photo photo, FormattedText caption)
        {
#if MODERN_TDLIB
            return new MessagePhoto(photo, null, caption, false, false, false);
#else
            return new MessagePhoto(photo, caption, false);
#endif
        }

        public static bool GetAdministratorCanDeleteMessages(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanDeleteMessages == true;
#else
            return administrator.CanDeleteMessages;
#endif
        }

        public static bool GetAdministratorCanEditMessages(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanEditMessages == true;
#else
            return administrator.CanEditMessages;
#endif
        }

        public static bool GetAdministratorCanInviteUsers(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanInviteUsers == true;
#else
            return administrator.CanInviteUsers;
#endif
        }

        public static bool GetAdministratorCanPinMessages(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanPinMessages == true;
#else
            return administrator.CanPinMessages;
#endif
        }

        public static bool GetAdministratorCanPostMessages(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanPostMessages == true;
#else
            return administrator.CanPostMessages;
#endif
        }

        public static bool GetAdministratorCanPromoteMembers(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanPromoteMembers == true;
#else
            return administrator.CanPromoteMembers;
#endif
        }

        public static bool GetAdministratorCanRestrictMembers(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.CanRestrictMembers == true;
#else
            return administrator.CanRestrictMembers;
#endif
        }

        public static bool GetAdministratorIsAnonymous(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return administrator.Rights?.IsAnonymous == true;
#else
            return administrator.IsAnonymous;
#endif
        }

        public static string GetAdministratorCustomTitle(ChatMemberStatusAdministrator administrator)
        {
#if MODERN_TDLIB
            return string.Empty;
#else
            return administrator.CustomTitle;
#endif
        }

        public static string GetAdministratorCustomTitle(ChatAdministrator administrator)
        {
            return administrator.CustomTitle;
        }

        public static string GetCreatorCustomTitle(ChatMemberStatusCreator creator)
        {
#if MODERN_TDLIB
            return string.Empty;
#else
            return creator.CustomTitle;
#endif
        }

        public static ChatMemberStatus CreateAdministratorStatus(bool canBeEdited, bool canChangeInfo, bool canDeleteMessages, bool canEditMessages, bool canInviteUsers, bool canPinMessages, bool canPostMessages, bool canPromoteMembers, bool canRestrictMembers, bool isAnonymous, string customTitle)
        {
#if MODERN_TDLIB
            var rights = new ChatAdministratorRights(
                true,
                canChangeInfo,
                canPostMessages,
                canEditMessages,
                canDeleteMessages,
                canInviteUsers,
                canRestrictMembers,
                canPinMessages,
                false,
                canPromoteMembers,
                false,
                false,
                false,
                false,
                false,
                false,
                isAnonymous);
            return new ChatMemberStatusAdministrator(canBeEdited, rights);
#else
            return new ChatMemberStatusAdministrator
            {
                IsAnonymous = isAnonymous,
                CanChangeInfo = canChangeInfo,
                CanDeleteMessages = canDeleteMessages,
                CanEditMessages = canEditMessages,
                CanInviteUsers = canInviteUsers,
                CanPinMessages = canPinMessages,
                CanPostMessages = canPostMessages,
                CanPromoteMembers = canPromoteMembers,
                CanRestrictMembers = canRestrictMembers,
                CustomTitle = customTitle ?? string.Empty
            };
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

    // The experimental app uses these small compatibility models for shared folder views.
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
        public ChatFolder Folder { get; set; }
        public string Description { get; set; }
    }

    public sealed class UpdateChatFilters
    {
        public IList<ChatFilterInfo> ChatFilters { get; set; } = new List<ChatFilterInfo>();
    }
}
