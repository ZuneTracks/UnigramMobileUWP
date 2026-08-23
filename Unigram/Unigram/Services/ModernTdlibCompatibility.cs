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
