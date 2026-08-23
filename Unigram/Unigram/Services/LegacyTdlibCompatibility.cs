using System.Collections.Generic;
using Telegram.Td.Api;

namespace Unigram.Services
{
    // Keeps shared sticker/event-log view models buildable against the legacy SDK.
    public static class ModernTdlibCompatibility
    {
        public static string GetWebPageTypeName(WebPage webPage)
        {
            return webPage?.Type;
        }

        public static TdlibParameters CreateTdlibParameters(bool useTestDc, string databaseDirectory, string filesDirectory, bool useFileDatabase, bool useChatInfoDatabase, bool useMessageDatabase, bool useSecretChats, int apiId, string apiHash, string systemLanguageCode, string deviceModel, string systemVersion, string applicationVersion, bool enableStorageOptimizer, bool ignoreFileNames)
        {
            return new TdlibParameters
            {
                UseTestDc = useTestDc,
                DatabaseDirectory = databaseDirectory,
                FilesDirectory = filesDirectory,
                UseFileDatabase = useFileDatabase,
                UseChatInfoDatabase = useChatInfoDatabase,
                UseMessageDatabase = useMessageDatabase,
                UseSecretChats = useSecretChats,
                ApiId = apiId,
                ApiHash = apiHash,
                SystemLanguageCode = systemLanguageCode,
                DeviceModel = deviceModel,
                SystemVersion = systemVersion,
                ApplicationVersion = applicationVersion,
                EnableStorageOptimizer = enableStorageOptimizer,
                IgnoreFileNames = ignoreFileNames
            };
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
            return new ToggleMessageSenderIsBlocked(sender, blocked);
        }

        public static MessageSendOptions CreateMessageSendOptions(bool disableNotification, bool fromBackground, MessageSchedulingState schedulingState)
        {
            return new MessageSendOptions(disableNotification, fromBackground, schedulingState);
        }

        public static Function GetInstalledStickerSets(bool masks)
        {
            return new GetInstalledStickerSets(masks);
        }

        public static Function GetRecentStickers(bool masks)
        {
            return new GetRecentStickers(masks);
        }

        public static Function GetArchivedStickerSets(bool masks, long offset, int limit)
        {
            return new GetArchivedStickerSets(masks, offset, limit);
        }

        public static Function ReorderInstalledStickerSets(bool masks, IList<long> stickerSetIds)
        {
            return new ReorderInstalledStickerSets(masks, stickerSetIds);
        }

        public static Function GetTrendingStickerSets()
        {
            return new GetTrendingStickerSets(0, 24);
        }

        public static Function GetStickers(string query, int limit)
        {
            return new GetStickers(query, limit);
        }

        public static Function SearchStickers(string query, int limit)
        {
            return new SearchStickers(query, limit);
        }

        public static StickerSetInfo CreateStickerSetInfo(long id, string title, string name, Thumbnail thumbnail, IList<ClosedVectorPath> thumbnailOutline, bool isOwned, bool isInstalled, bool isArchived, bool isOfficial, bool isAnimated, bool isMasks, bool isViewed, int size, IList<Sticker> covers)
        {
            return new StickerSetInfo(id, title, name, thumbnail, thumbnailOutline, isInstalled, isArchived, isOfficial, isAnimated, isMasks, isViewed, size, covers);
        }

        public static ChatPermissions CreateChatPermissions(bool allowed)
        {
            return new ChatPermissions(allowed, allowed, allowed, allowed, allowed, allowed, allowed, allowed);
        }

        public static InputMessageContent CreateInputMessageDocument(InputFile file, InputThumbnail thumbnail, bool disableContentTypeDetection, FormattedText caption)
        {
            return new InputMessageDocument(file, thumbnail, disableContentTypeDetection, caption);
        }

        public static InputMessageContent CreateInputMessagePhoto(InputFile file, InputThumbnail thumbnail, int width, int height, FormattedText caption, int ttl)
        {
            return new InputMessagePhoto(file, thumbnail, new int[0], width, height, caption, ttl);
        }

        public static InputMessageContent CreateInputMessageAnimation(InputFile file, InputThumbnail thumbnail, int duration, int width, int height, FormattedText caption)
        {
            return new InputMessageAnimation(file, thumbnail, new int[0], duration, width, height, caption);
        }

        public static InputMessageContent CreateInputMessageVideo(InputFile file, InputThumbnail thumbnail, int duration, int width, int height, FormattedText caption, int ttl)
        {
            return new InputMessageVideo(file, thumbnail, new int[0], duration, width, height, true, caption, ttl);
        }

        public static InputMessageContent CreateInputMessageVideoNote(InputFile file, InputThumbnail thumbnail, int duration, int length)
        {
            return new InputMessageVideoNote(file, thumbnail, duration, length);
        }

        public static InputMessageContent CreateInputMessageSticker(InputFile file, InputThumbnail thumbnail, int width, int height, string emoji)
        {
            return new InputMessageSticker(file, thumbnail, width, height, emoji);
        }

        public static InputMessageContent CreateInputMessageAudio(InputFile file, InputThumbnail thumbnail, int duration, string title, string performer, FormattedText caption)
        {
            return new InputMessageAudio(file, thumbnail, duration, title, performer, caption);
        }

        public static InputMessageContent CreateInputMessageVoiceNote(InputFile file, int duration, FormattedText caption)
        {
            return new InputMessageVoiceNote(file, duration, new byte[0], caption);
        }

        public static InputMessageContent CreateInputMessagePoll(string question, IList<string> options, bool isAnonymous, PollType type, bool allowsMultipleAnswers)
        {
            return new InputMessagePoll(question, options, isAnonymous, type, 0, 0, false);
        }

        public static PollType CreatePollTypeQuiz(int correctOptionId, FormattedText explanation)
        {
            return new PollTypeQuiz(correctOptionId, explanation);
        }

        public static PollType CreatePollTypeRegular(bool allowsMultipleAnswers)
        {
            return new PollTypeRegular(allowsMultipleAnswers);
        }

        public static InputMessageContent CreateInputMessageLocation(Location location)
        {
            return new InputMessageLocation(location, 0, 0, 0);
        }

        public static MessageContent CreateMessagePhoto(Photo photo, FormattedText caption)
        {
            return new MessagePhoto(photo, caption, false);
        }

        public static bool GetAdministratorCanChangeInfo(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanChangeInfo;
        }

        public static bool GetAdministratorCanDeleteMessages(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanDeleteMessages;
        }

        public static bool GetAdministratorCanEditMessages(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanEditMessages;
        }

        public static bool GetAdministratorCanInviteUsers(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanInviteUsers;
        }

        public static bool GetAdministratorCanPinMessages(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanPinMessages;
        }

        public static bool GetAdministratorCanPostMessages(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanPostMessages;
        }

        public static bool GetAdministratorCanPromoteMembers(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanPromoteMembers;
        }

        public static bool GetAdministratorCanRestrictMembers(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CanRestrictMembers;
        }

        public static bool GetAdministratorIsAnonymous(ChatMemberStatusAdministrator administrator)
        {
            return administrator.IsAnonymous;
        }

        public static string GetAdministratorCustomTitle(ChatMemberStatusAdministrator administrator)
        {
            return administrator.CustomTitle;
        }

        public static string GetAdministratorCustomTitle(ChatAdministrator administrator)
        {
            return administrator.CustomTitle;
        }

        public static string GetCreatorCustomTitle(ChatMemberStatusCreator creator)
        {
            return creator.CustomTitle;
        }

        public static ChatMemberStatus CreateAdministratorStatus(bool canBeEdited, bool canChangeInfo, bool canDeleteMessages, bool canEditMessages, bool canInviteUsers, bool canPinMessages, bool canPostMessages, bool canPromoteMembers, bool canRestrictMembers, bool isAnonymous, string customTitle)
        {
            return new ChatMemberStatusAdministrator
            {
                CanBeEdited = canBeEdited,
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
        }
    }
}
