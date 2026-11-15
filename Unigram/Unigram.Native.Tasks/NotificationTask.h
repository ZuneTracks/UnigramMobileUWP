#pragma once

#include <ios>
#include <fstream>
#include <ctime>
#include <ppltasks.h>

using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::ApplicationModel::Background;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::Networking::PushNotifications;
using namespace Windows::Foundation;

namespace Unigram
{
	namespace Native 
	{
		namespace Tasks
		{
			[Windows::Foundation::Metadata::WebHostHidden]
			public ref class NotificationTask sealed : public IBackgroundTask
			{
			public:
				NotificationTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

				static void UpdatePrimaryBadge(int badgeNumber);
				static void UpdateBadgeFromPush(String^ session, String^ loc_key, JsonObject^ custom, String^ group, bool muted);
				static void UpdatePrimaryTile(String^ launch, String^ caption, String^ message, String^ picture);
				static IAsyncAction^ UpdateToast(String^ caption, String^ message, String^ attribution, String^ session, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ hero, String^ date, String^ loc_key);

			private:
				static IAsyncAction^ ShowIncomingCallToast(String^ caption, String^ message, String^ account, String^ launch, String^ group, String^ picture, String^ date, time_t expires);
				static void RemoveCallToast(String^ account, String^ group);
				concurrency::task<void> UpdateToastAndTiles(String^ content /*, std::wofstream* log*/);
				String^ GetCaption(JsonArray^ loc_args, String^ loc_key);
				String^ GetMessage(JsonArray^ loc_args, String^ loc_key);
				String^ GetLaunch(JsonObject^ custom, String^ loc_key, String^ session);
				String^ GetTag(JsonObject^ custom);
				String^ GetGroup(JsonObject^ custom);
				String^ GetPicture(JsonObject^ custom, String^ group, String^ session);
				String^ GetDate(JsonObject^ notification);
				String^ GetSession(long long receiverId);
				static bool IsSecretChatPush(String^ loc_key);
				static bool ShowSecretChatOnLiveTiles(String^ session, bool sessionMapped);

				static void LogError(String^ operation, Exception^ exception);
				static void LogDiagnostic(String^ eventName, String^ details);
				static std::wstring Escape(std::wstring data);
				static std::wstring TruncateTileText(std::wstring data, size_t maximum);
			};
		}
	}
}
