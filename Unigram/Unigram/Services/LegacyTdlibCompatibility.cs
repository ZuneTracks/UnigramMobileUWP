using System.Collections.Generic;
using Telegram.Td.Api;

namespace Unigram.Services
{
    // Keeps shared sticker/event-log view models buildable against the legacy SDK.
    public static class ModernTdlibCompatibility
    {
        public static Function SetMessageSenderBlocked(MessageSender sender, bool blocked)
        {
            return new ToggleMessageSenderIsBlocked(sender, blocked);
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
    }
}
