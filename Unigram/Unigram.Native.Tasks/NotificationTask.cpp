#include "pch.h"
#include "NotificationTask.h"

#include <ios>
#include <fstream>

#include <algorithm>
#include <experimental\resumable>
#include <ppltasks.h>
#include <pplawait.h>
#include <iostream>  
#include <iomanip>
#include <sstream>
#include <ctime>
#include <windows.h>
#include "Shlwapi.h"

using namespace concurrency;
using namespace Platform;
using namespace Telegram::Td;
using namespace Unigram::Native::Tasks;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::ApplicationModel::Resources;
using namespace Windows::Data::Json;
using namespace Windows::Data::Xml::Dom;
using namespace Windows::Foundation;
using namespace Windows::Storage;
using namespace Windows::UI::Notifications;
using namespace Windows::UI::StartScreen;

namespace
{
	// Telegram's ring timeout is 90 seconds; a call push older than this cannot still be
	// ringing on the caller's side, so it is shown as an ordinary notification instead.
	const time_t CallRingWindowSeconds = 60;
	const time_t CallToastLifetimeSeconds = 90;

	// loc_key is a server-defined constant (for example PHONE_CALL_REQUEST). Only uppercase
	// letters, digits and underscores are kept so no payload content can reach the log.
	String^ SanitizePushType(String^ loc_key)
	{
		if (loc_key == nullptr || loc_key->Length() == 0)
		{
			return L"none";
		}

		std::wstring result;
		for (auto ch : std::wstring(loc_key->Data()))
		{
			if (result.size() >= 48)
			{
				break;
			}

			if ((ch >= L'A' && ch <= L'Z') || (ch >= L'0' && ch <= L'9') || ch == L'_')
			{
				result += ch;
			}
			else
			{
				result += L'?';
			}
		}

		return ref new String(result.c_str());
	}

	DateTime FromUnixTime(time_t value)
	{
		DateTime result;
		result.UniversalTime = (static_cast<int64>(value) + 11644473600LL) * 10000000LL;
		return result;
	}
}

void NotificationTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	auto deferral = taskInstance->GetDeferral();
	LogDiagnostic(L"native.task.run", L"entrypoint=Unigram.Native.Tasks.NotificationTask");
	auto details = dynamic_cast<RawNotification^>(taskInstance->TriggerDetails);

	if (details == nullptr || details->Content == nullptr)
	{
		OutputDebugStringW(L"[Notifications] Push task activated without a raw notification payload.\r\n");
		LogDiagnostic(L"native.task.trigger.invalid", L"trigger=not_raw_or_empty");
		deferral->Complete();
		return;
	}

	LogDiagnostic(L"native.raw.received", ref new String((L"length=" + std::to_wstring(details->Content->Length())).c_str()));
	UpdateToastAndTiles(details->Content).then([deferral](task<void> completed)
	{
		try
		{
			completed.get();
			LogDiagnostic(L"native.raw.processed", L"result=success;tdlib_processing=not_applicable");
		}
		catch (Exception ^ ex)
		{
			LogError(L"processing raw push notification", ex);
		}

		deferral->Complete();
		LogDiagnostic(L"native.task.complete", nullptr);
	});
}

task<void> NotificationTask::UpdateToastAndTiles(String^ content /*, std::wofstream* log*/)
{
	return create_task([=] {
		auto notification = JsonValue::Parse(content)->GetObject();
		auto data = notification->GetNamedObject("data");
		if (data == nullptr)
		{
			LogDiagnostic(L"native.payload.parsed", L"result=no_data");
			return;
		}
		LogDiagnostic(L"native.payload.parsed", L"result=success;has_data=true");

		String^ session = nullptr;
		try
		{
			auto receiver = dynamic_cast<Telegram::Td::Api::PushReceiverId^>(
				Client::Execute(ref new Telegram::Td::Api::GetPushReceiverId(content)));
			if (receiver != nullptr)
			{
				session = GetSession(receiver->Id);
				LogDiagnostic(L"native.receiver.resolve", L"result=success");

				// A real Telegram push arrived, so the managed periodic sync can pause itself.
				try
				{
					ApplicationData::Current->LocalSettings->Values->Insert(L"LastTelegramPushTime", PropertyValue::CreateInt64(static_cast<int64>(std::time(nullptr))));
				}
				catch (Exception ^ ex)
				{
					LogError(L"recording push time", ex);
				}
			}
			else
			{
				LogDiagnostic(L"native.receiver.resolve", L"result=missing");
			}
		}
		catch (Exception ^ ex)
		{
			LogError(L"resolving push receiver", ex);
			LogDiagnostic(L"native.receiver.resolve", L"result=error");
		}

		// Older payloads can contain the account id directly. This remains a
		// compatibility fallback only; TDLib's parser above is authoritative for
		// modern encrypted raw pushes.
		if (session == nullptr && data->HasKey(L"user_id"))
		{
			session = GetSession(static_cast<long long>(data->GetNamedNumber(L"user_id")));
		}
		auto sessionMapped = session != nullptr;
		if (session == nullptr)
		{
			session = L"0";
			LogDiagnostic(L"native.session.resolve", L"result=fallback_default");
		}
		else
		{
			LogDiagnostic(L"native.session.resolve", L"result=mapped");
		}

		auto muted = data->GetNamedString("mute", "0") == L"1";
		{
			std::wstring details = L"type=";
			details += SanitizePushType(data->GetNamedString("loc_key", L""))->Data();
			details += muted ? L";mute=1" : L";mute=0";
			LogDiagnostic(L"native.payload.type", ref new String(details.c_str()));
		}

		if (data->HasKey("loc_key") == false)
		{
			//time_t rawtime = time(NULL);
			//struct tm timeinfo;
			//wchar_t buffer[80];

			//time(&rawtime);
			//localtime_s(&timeinfo, &rawtime);

			//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
			//std::wstring str(buffer);

			//*log << L"[";
			//*log << str;
			//*log << L"] Removing a toast notification\n";

			auto custom = data->GetNamedObject("custom");
			auto group = GetGroup(custom);

			if (group != nullptr)
			{
				ToastNotificationManager::History->RemoveGroup(group, L"App");
			}
			LogDiagnostic(L"native.notification.removed", L"result=success");
			return;
		}

		auto pushType = data->GetNamedString("loc_key");
		// Video calls use their own loc_keys; without them a video call was dropped as muted.
		auto isCallRequest = pushType->Equals(L"PHONE_CALL_REQUEST") || pushType->Equals(L"VIDEO_CALL_REQUEST");
		if (pushType->Equals(L"PHONE_CALL_MISSED") || pushType->Equals(L"VIDEO_CALL_MISSED"))
		{
			// The call is over: stop the ringing toast even if the chat is muted.
			RemoveCallToast(session, GetGroup(data->GetNamedObject("custom", nullptr)));
			ClearPendingCallPush();
		}

		if (sessionMapped)
		{
			// Must run before the toast is shown: in chat-count mode an existing toast group means the chat was already counted.
			UpdateBadgeFromPush(session, pushType, data->GetNamedObject("custom", nullptr), GetGroup(data->GetNamedObject("custom", nullptr)), muted);
		}

		// Chat mute silences messages, not calls: Telegram marks call pushes from muted
		// chats with mute=1, and suppressing them meant incoming calls never rang.
		if (!muted || isCallRequest)
		{
			auto loc_key = data->GetNamedString("loc_key");
			auto loc_args = data->GetNamedArray("loc_args");
			auto custom = data->GetNamedObject("custom", nullptr);

			//time_t rawtime = time(NULL);
			//struct tm timeinfo;
			//wchar_t buffer[80];

			//time(&rawtime);
			//localtime_s(&timeinfo, &rawtime);

			//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
			//std::wstring str(buffer);

			//*log << L"[";
			//*log << str;
			//*log << L"] Received notification with loc_key ";
			//*log << loc_key->Data();
			//*log << L"\n";

			auto caption = GetCaption(loc_args, loc_key);
			auto message = GetMessage(loc_args, loc_key);
			auto sound = data->GetNamedString("sound", "Default");
			auto launch = GetLaunch(custom, loc_key, sessionMapped ? session : nullptr);
			auto group = GetGroup(custom);
			auto isSecretChat = IsSecretChatPush(loc_key);
			String^ picture = isSecretChat ? nullptr : GetPicture(custom, group, session);
			LogPictureSource(picture);
			auto date = GetDate(notification);

			if (message == nullptr)
			{
				message = data->GetNamedString("text", "New Notification");
			}

			if (isCallRequest)
			{
				auto pushTime = static_cast<time_t>(notification->GetNamedNumber(L"date", 0));
				auto now = std::time(nullptr);
				LogCallPushShape(custom, pushTime > 0 ? static_cast<long long>(now - pushTime) : 0);
				if (pushTime > 0 && now - pushTime <= CallRingWindowSeconds)
				{
					if (sessionMapped)
					{
						// The device receive time, not the server date: the two clocks can differ by
						// a few seconds, which made the app reject fresh pushes as "expired".
						SavePendingCallPush(content, session, now);
					}

					create_task(ShowIncomingCallToast(caption, message, session, launch, group, picture, date, pushTime + CallToastLifetimeSeconds)).get();
					LogDiagnostic(L"native.notification.displayed", muted ? L"toast=true;kind=call;ring=1;muted=1" : L"toast=true;kind=call;ring=1;muted=0");
				}
				else
				{
					create_task(UpdateToast(caption, message, session, session, sound, launch, L"phoneCall", group, picture, nullptr, date, loc_key)).get();
					LogDiagnostic(L"native.notification.displayed", L"toast=true;kind=call;ring=0;reason=stale");
				}
			}
			else
			{
				auto tag = GetTag(custom);
				create_task(UpdateToast(caption, message, session, session, sound, launch, tag, group, picture, nullptr, date, loc_key)).get();
				if (!isSecretChat || ShowSecretChatOnLiveTiles(session, sessionMapped))
				{
					UpdatePrimaryTile(launch, caption, message, picture);
				}
				else
				{
					LogDiagnostic(L"native.tile.decision", L"result=skipped;reason=secret_chat_setting");
				}
				LogDiagnostic(L"native.notification.displayed", L"toast=true");
			}
		}
		else
		{
			LogDiagnostic(L"native.notification.suppressed", L"reason=muted");
		}
	});
}

String^ NotificationTask::GetCaption(JsonArray^ loc_args, String^ loc_key)
{
	std::wstring key = loc_key->Data();

	if (key.find(L"CHAT") == 0 || key.find(L"GEOCHAT") == 0)
	{
		return loc_args->GetStringAt(1);
	}
	else if (key.find(L"MESSAGE") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"CHANNEL") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"PINNED") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"PHONE_CALL") == 0 || key.find(L"VIDEO_CALL") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"AUTH") == 0 || key.find(L"CONTACT") == 0 || key.find(L"ENCRYPTED") == 0 || key.find(L"ENCRYPTION") == 0)
	{
		return "Telegram";
	}

	return "Telegram";
}

String^ NotificationTask::GetMessage(JsonArray^ loc_args, String^ loc_key)
{
	auto resourceLoader = ResourceLoader::GetForViewIndependentUse("Unigram.Native.Tasks/Resources");
	auto text = resourceLoader->GetString(loc_key);
	if (text->Length())
	{
		std::wstring wtext = text->Data();

		for (int i = 0; i < loc_args->Size; i++)
		{
			wchar_t* code = new wchar_t[4];
			swprintf_s(code, 4, L"{%d}", i);
			std::string::size_type index = wtext.find(code);
			if (index != std::string::npos)
			{
				wtext = wtext.replace(wtext.find(code), 3, loc_args->GetStringAt(i)->Data());
			}
		}

		return ref new String(wtext.c_str());
	}

	return nullptr;
}

String^ NotificationTask::GetLaunch(JsonObject^ custom, String^ loc_key, String^ session)
{
	std::wstring launch = L"";
	// Without the owning account, activation (open chat, quick reply, mark as read)
	// falls back to whichever account is active. Omit it when the receiver was not
	// mapped rather than guessing account 0.
	if (session != nullptr)
	{
		launch += L"session=";
		launch += session->Data();
		launch += L"&amp;";
	}
	if (custom)
	{
		if (custom->HasKey("msg_id"))
		{
			launch += L"msg_id=";
			launch += custom->GetNamedString("msg_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("chat_id"))
		{
			launch += L"chat_id=";
			launch += custom->GetNamedString("chat_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("channel_id"))
		{
			launch += L"channel_id=";
			launch += custom->GetNamedString("channel_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("from_id"))
		{
			launch += L"from_id=";
			launch += custom->GetNamedString("from_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("mtpeer"))
		{
			auto mtpeer = custom->GetNamedObject("mtpeer");
			if (mtpeer->HasKey("ah"))
			{
				launch += L"access_hash=";
				launch += mtpeer->GetNamedString("ah")->Data();
				launch += L"&amp;";
			}
		}
	}

	launch += L"Action=";
	launch += loc_key->Data();

	return ref new String(launch.c_str());
}

String^ NotificationTask::GetTag(JsonObject^ custom)
{
	if (custom)
	{
		return custom->GetNamedString("msg_id", nullptr);
	}

	return nullptr;
}

String^ NotificationTask::GetGroup(JsonObject^ custom)
{
	if (custom)
	{
		if (custom->HasKey("chat_id"))
		{
			return String::Concat("c", custom->GetNamedString("chat_id"));
		}
		else if (custom->HasKey("channel_id"))
		{
			return String::Concat("c", custom->GetNamedString("channel_id"));
		}
		else if (custom->HasKey("from_id"))
		{
			return String::Concat("u", custom->GetNamedString("from_id"));
		}
		else if (custom->HasKey("contact_id"))
		{
			return String::Concat("u", custom->GetNamedString("contact_id"));
		}
	}

	return nullptr;
}

String^ NotificationTask::GetPicture(JsonObject^ custom, String^ group, String^ session)
{
	if (custom && custom->HasKey("mtpeer"))
	{
		auto mtpeer = custom->GetNamedObject("mtpeer");
		if (mtpeer->HasKey("ph"))
		{
			auto ph = mtpeer->GetNamedObject("ph");
			if (ph->HasKey("_layers") && ph->HasKey("1"))
			{
				ph = ph->GetNamedObject("1");
			}

			auto volume_id = ph->GetNamedString("volume_id");
			auto local_id = ph->GetNamedString("local_id");

			std::wstring volumeSTR = volume_id->Data();
			auto volumeULL = wcstoull(volumeSTR.c_str(), NULL, 0);
			auto volume = static_cast<signed long long>(volumeULL);

			auto temp = ApplicationData::Current->LocalFolder->Path;

			if (group == nullptr)
			{
				return nullptr;
			}

			// Must match SessionStorage.Root on the managed side: the modern TDLib build
			// isolates per-session storage, and the avatar is stored under the session that
			// actually received the push, never an assumed session 0.
			std::wstring root = L"ms-appdata:///local/";
#ifdef MODERN_TDLIB
			root += L"tdlib-experimental/";
#endif

			std::wstringstream almost;
			almost << root
				<< (session != nullptr ? session->Data() : L"0")
				<< L"/profile_photos/"
				<< volume
				<< L"_"
				<< local_id->Data()
				<< L".jpg";

			return ref new String(almost.str().c_str());
		}
	}

	if (group == nullptr)
	{
		return nullptr;
	}

	std::wstringstream almost;
	almost << L"ms-appdata:///local/temp/"
		<< group->Data()
		<< L"_placeholder.png";

	return ref new String(almost.str().c_str());
}

// Records only which kind of avatar a toast references and whether that file exists; a
// missing file makes the toast render without an avatar.
void NotificationTask::LogPictureSource(String^ picture)
{
	if (picture == nullptr)
	{
		LogDiagnostic(L"native.toast.picture", L"source=none");
		return;
	}

	std::wstring uri = picture->Data();
	const std::wstring prefix = L"ms-appdata:///local/";
	const wchar_t* source = uri.find(L"/profile_photos/") != std::wstring::npos ? L"photo" : L"placeholder";
	bool exists = false;

	if (uri.compare(0, prefix.length(), prefix) == 0)
	{
		std::wstring path = ApplicationData::Current->LocalFolder->Path->Data();
		path += L"\\";
		path += uri.substr(prefix.length());
		std::replace(path.begin(), path.end(), L'/', L'\\');
		exists = GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
	}

	std::wstring details = L"source=";
	details += source;
	details += exists ? L";exists=1" : L";exists=0";
	LogDiagnostic(L"native.toast.picture", ref new String(details.c_str()));
}

String^ NotificationTask::GetDate(JsonObject^ notification)
{
	const time_t rawtime = notification->GetNamedNumber(L"date");
	struct tm dt;
	wchar_t buffer[30];
	gmtime_s(&dt, &rawtime);
	wcsftime(buffer, sizeof(buffer), L"%FT%TZ", &dt);
	return ref new String(buffer);
}

String^ NotificationTask::GetSession(long long receiverId)
{
	// TDLib identifiers are signed 53-bit integers. The managed background handler
	// resolves the push-receiver mapping first; the native task must use the same
	// lookup order and must not truncate a modern account id to 32 bits.
	if (receiverId == 0)
	{
		return nullptr;
	}

	auto values = ApplicationData::Current->LocalSettings->Values;
	std::wstringstream key;
	key << L"PushReceiverId" << receiverId;
	auto receiverKey = ref new String(key.str().c_str());

	if (values->HasKey(receiverKey))
	{
		return values->Lookup(receiverKey)->ToString();
	}

	key.str(L"");
	key.clear();
	key << L"User" << receiverId;
	auto userKey = ref new String(key.str().c_str());

	if (values->HasKey(userKey))
	{
		return values->Lookup(userKey)->ToString();
	}

	return nullptr;
}

bool NotificationTask::IsSecretChatPush(String^ loc_key)
{
	std::wstring key = loc_key->Data();
	return key.find(L"ENCRYPTED") == 0 || key.find(L"ENCRYPTION") == 0;
}

bool NotificationTask::ShowSecretChatOnLiveTiles(String^ session, bool sessionMapped)
{
	// Mirrors NotificationsSettings.ShowSecretChatOnLiveTiles: session 0 uses the root
	// LocalSettings, other sessions use the "{session}" container. Defaults to hidden.
	if (!sessionMapped || session == nullptr)
	{
		return false;
	}

	try
	{
		auto settings = ApplicationData::Current->LocalSettings;
		Windows::Foundation::Collections::IPropertySet^ values = nullptr;

		if (session->Equals(L"0"))
		{
			values = settings->Values;
		}
		else if (settings->Containers->HasKey(session))
		{
			values = settings->Containers->Lookup(session)->Values;
		}

		if (values != nullptr && values->HasKey(L"ShowSecretChatOnLiveTiles"))
		{
			auto value = dynamic_cast<IBox<bool>^>(values->Lookup(L"ShowSecretChatOnLiveTiles"));
			return value != nullptr && value->Value;
		}
	}
	catch (Exception ^ ex)
	{
		LogError(L"reading secret chat tile setting", ex);
	}

	return false;
}

void NotificationTask::UpdatePrimaryBadge(int badgeNumber)
{
	try
	{
		auto updater = BadgeUpdateManager::CreateBadgeUpdaterForApplication(L"App");

		if (badgeNumber == 0)
		{
			updater->Clear();
			return;
		}

		auto document = BadgeUpdateManager::GetTemplateContent(BadgeTemplateType::BadgeNumber);
		auto element = safe_cast<XmlElement^>(document->SelectSingleNode("/badge"));
		element->SetAttribute("value", badgeNumber.ToString());

		updater->Update(ref new BadgeNotification(document));
	}
	catch (Exception ^ ex)
	{
		LogError(L"updating primary badge", ex);
	}
}

void NotificationTask::UpdateBadgeFromPush(String^ session, String^ loc_key, JsonObject^ custom, String^ group, bool muted)
{
	try
	{
		// The managed app records the real unread count and the active account's badge
		// preferences whenever it updates the badge; pushes only add to that baseline.
		auto values = ApplicationData::Current->LocalSettings->Values;
		if (!values->HasKey(L"BadgeCount") || !values->HasKey(L"BadgeSession"))
		{
			LogDiagnostic(L"native.badge", L"result=skipped;reason=no_baseline");
			return;
		}

		auto badgeSession = safe_cast<int>(values->Lookup(L"BadgeSession"));
		if (session == nullptr || !session->Equals(ref new String(std::to_wstring(badgeSession).c_str())))
		{
			LogDiagnostic(L"native.badge", L"result=skipped;reason=other_account");
			return;
		}

		std::wstring key = loc_key->Data();
		auto isMessage = key.find(L"MESSAGE") == 0 || key.find(L"CHAT_") == 0 || key.find(L"CHANNEL_") == 0
			|| key.find(L"PINNED_") == 0 || key.find(L"ENCRYPTED_MESSAGE") == 0 || key.find(L"CONTACT_JOINED") == 0;
		if (!isMessage || key.find(L"REACT") != std::wstring::npos || (custom != nullptr && custom->HasKey(L"edit_date")))
		{
			LogDiagnostic(L"native.badge", L"result=skipped;reason=not_new_message");
			return;
		}

		auto includeMuted = values->HasKey(L"BadgeIncludeMuted") && safe_cast<bool>(values->Lookup(L"BadgeIncludeMuted"));
		if (muted && !includeMuted)
		{
			LogDiagnostic(L"native.badge", L"result=skipped;reason=muted");
			return;
		}

		auto countMessages = !values->HasKey(L"BadgeCountMessages") || safe_cast<bool>(values->Lookup(L"BadgeCountMessages"));
		if (!countMessages)
		{
			// Counting chats: only a chat without a toast on screen is new to the count. Muted
			// chats show no toast, so they can't be tracked and are left to the app's next update.
			if (group == nullptr || muted)
			{
				LogDiagnostic(L"native.badge", L"result=skipped;reason=chat_untracked");
				return;
			}

			auto history = ToastNotificationManager::History->GetHistory(L"App");
			for (unsigned int i = 0; i < history->Size; i++)
			{
				if (group->Equals(history->GetAt(i)->Group))
				{
					LogDiagnostic(L"native.badge", L"result=skipped;reason=chat_already_counted");
					return;
				}
			}
		}

		auto count = safe_cast<int>(values->Lookup(L"BadgeCount")) + 1;
		values->Insert(L"BadgeCount", PropertyValue::CreateInt32(count));
		UpdatePrimaryBadge(count);
		LogDiagnostic(L"native.badge", countMessages ? L"result=updated;mode=messages" : L"result=updated;mode=chats");
	}
	catch (Exception ^ ex)
	{
		LogError(L"updating badge from push", ex);
	}
}

void NotificationTask::UpdatePrimaryTile(String^ launch, String^ caption, String^ message, String^ picture)
{
	try
	{
		std::wstring image;
		if (picture != nullptr && picture->Length() > 0)
		{
			image = L"<image hint-crop='circle' src='";
			image += Escape(picture->Data());
			image += L"'/>";
		}

		// The body is repeated in three bindings and the payload limit is 5 KB.
		std::wstring body = L"<text hint-style='body'>";
		body += Escape(TruncateTileText(caption->Data(), 48));
		body += L"</text><text hint-style='captionSubtle' hint-wrap='true'>";
		body += Escape(TruncateTileText(message->Data(), 160));
		body += L"</text>";

		std::wstring xml = L"<tile><visual arguments='";
		xml += launch->Data();
		xml += L"'><binding template='TileMedium' branding='name'>";
		xml += image;
		xml += body;
		xml += L"</binding><binding template='TileWide' branding='nameAndLogo'><group><subgroup hint-weight='18'>";
		xml += image;
		xml += L"</subgroup><subgroup>";
		xml += body;
		xml += L"</subgroup></group></binding><binding template='TileLarge' branding='nameAndLogo'><group><subgroup hint-weight='18'>";
		xml += image;
		xml += L"</subgroup><subgroup>";
		xml += body;
		xml += L"</subgroup></group></binding></visual></tile>";

		auto document = ref new XmlDocument();
		document->LoadXml(ref new String(xml.c_str()));

		auto updater = TileUpdateManager::CreateTileUpdaterForApplication(L"App");
		updater->EnableNotificationQueue(false);
		updater->Update(ref new TileNotification(document));
		LogDiagnostic(L"native.tile.update", L"result=success");
	}
	catch (Exception ^ ex)
	{
		LogError(L"updating primary Live Tile", ex);
	}
}

void NotificationTask::LogError(String^ operation, Exception^ exception)
{
	std::wstring message = L"[Notifications] Error ";
	message += operation->Data();
	message += L": ";
	message += exception->Message->Data();
	message += L"\r\n";
	OutputDebugStringW(message.c_str());

	std::wstringstream details;
	details << L"operation=" << operation->Data() << L";result=error;hresult=0x" << std::hex << exception->HResult;
	LogDiagnostic(L"native.task.error", ref new String(details.str().c_str()));
}

void NotificationTask::LogDiagnostic(String^ eventName, String^ details)
{
	try
	{
		auto root = ApplicationData::Current->LocalFolder->Path;
		std::wstring directory = root->Data();
		directory += L"\\Diagnostics";
		CreateDirectoryW(directory.c_str(), nullptr);

		std::wstring path = directory + L"\\push-diagnostics.txt";

		// Check file size and truncate if over 1 MB
		{
			std::ifstream sizeCheck(path, std::ios::ate | std::ios::binary);
			if (sizeCheck.is_open() && sizeCheck.tellg() >= 1024 * 1024)
			{
				sizeCheck.close();
				std::ofstream truncate(path, std::ios::trunc);
			}
		}

		SYSTEMTIME now;
		GetSystemTime(&now);
		std::wstringstream line;
		line << std::setfill(L'0')
			<< std::setw(4) << now.wYear << L"-"
			<< std::setw(2) << now.wMonth << L"-"
			<< std::setw(2) << now.wDay << L"T"
			<< std::setw(2) << now.wHour << L":"
			<< std::setw(2) << now.wMinute << L":"
			<< std::setw(2) << now.wSecond << L"."
			<< std::setw(3) << now.wMilliseconds << L"Z|"
			<< eventName->Data();
		if (details != nullptr && details->Length() > 0)
		{
			line << L"|" << details->Data();
		}
		line << L"\r\n";

		auto wide = line.str();
		auto length = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.length()), nullptr, 0, nullptr, nullptr);
		std::string utf8(length, '\0');
		WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.length()), &utf8[0], length, nullptr, nullptr);

		std::ofstream file(path, std::ios::app | std::ios::binary);
		if (file.is_open())
		{
			file.write(utf8.data(), utf8.size());
		}
	}
	catch (...)
	{
		OutputDebugStringW(L"[Notifications] Unable to write persistent diagnostics.\r\n");
	}
}

std::wstring NotificationTask::TruncateTileText(std::wstring data, size_t maximum)
{
	if (data.size() <= maximum)
	{
		return data;
	}

	auto length = maximum;
	if (data[length - 1] >= 0xD800 && data[length - 1] <= 0xDBFF)
	{
		length--;
	}

	return data.substr(0, length) + L"\u2026";
}

std::wstring NotificationTask::Escape(std::wstring data)
{
	std::wstring buffer;
	buffer.reserve(data.size());
	for (size_t pos = 0; pos != data.size(); ++pos)
	{
		switch (data[pos])
		{
		case '&':  buffer.append(L"&amp;");       break;
		case '\"': buffer.append(L"&quot;");      break;
		case '\'': buffer.append(L"&apos;");      break;
		case '<':  buffer.append(L"&lt;");        break;
		case '>':  buffer.append(L"&gt;");        break;
		default:   buffer.append(&data[pos], 1); break;
		}
	}
	return buffer;
}

Windows::Foundation::IAsyncAction^ NotificationTask::UpdateToast(String^ caption, String^ message, String^ attribution, String^ account, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ hero, String^ date, String^ loc_key)
{
	return create_async([=]()
	{
		bool allow = true;
		//auto settings = ApplicationData::Current->LocalSettings;
		//if (settings->Values->HasKey("SessionGuid"))
		//{
		//	auto guid = safe_cast<String^>(settings->Values->Lookup("SessionGuid"));

		//	std::wstringstream path;
		//	path << temp->Data()
		//		<< L"\\"
		//		<< guid->Data()
		//		<< L"\\passcode_params.dat";

		//	WIN32_FIND_DATA FindFileData;
		//	HANDLE handle = FindFirstFile(path.str().c_str(), &FindFileData);
		//	int found = handle != INVALID_HANDLE_VALUE;
		//	if (found)
		//	{
		//		FindClose(handle);

		//		allow = false;
		//	}
		//}

		std::wstring key = loc_key->Data();
		std::wstring actions = L"";
		if (group != nullptr && key.find(L"CHANNEL") != 0 && allow)
		{
			actions = L"<actions><input id='input' type='text' placeHolderContent='ms-resource:Reply' /><action activationType='background' arguments='action=markAsRead&amp;";
			actions += launch->Data();
			//actions += L"' hint-inputId='QuickMessage' content='ms-resource:Send' imageUri='ms-appx:///Assets/Icons/Toast/Send.png'/></actions>";
			actions += L"' content='ms-resource:MarkAsRead'/><action activationType='background' arguments='action=reply&amp;";
			actions += launch->Data();
			actions += L"' content='ms-resource:Send'/></actions>";
		}

		std::wstring audio = L"";
		if (sound->Equals("silent"))
		{
			audio = L"<audio silent='true'/>";
		}

		std::wstring xml = L"<toast launch='";
		xml += launch->Data();
		xml += L"' displayTimestamp='";
		xml += date->Data();
		//xml += L"' hint-people='remoteid:";
		//xml += group->Data();
		xml += L"'>";
		//xml += L"<header id='";
		//xml += account->Data();
		//xml += L"' title='Camping!!' arguments='action = openConversation & amp; id = 6289'/>";
		xml += L"<visual><binding template='ToastGeneric'>";

		if (picture != nullptr)
		{
			xml += L"<image placement='appLogoOverride' hint-crop='circle' src='";
			xml += picture->Data();
			xml += L"'/>";
		}

		xml += L"<text><![CDATA[";
		xml += caption->Data();
		xml += L"]]></text><text><![CDATA[";
		xml += message->Data();
		//xml += L"]]></text><text placement='attribution'>Unigram</text></binding></visual>";
		xml += L"]]></text>";

		if (hero != nullptr && hero->Length())
		{
			xml += L"<image src='";
			xml += hero->Data();
			xml += L"'/>";
		}

		//xml += L"<text placement='attribution'><![CDATA[";
		//xml += attribution->Data();
		//xml += L"]]></text>";
		xml += L"</binding></visual>";
		xml += actions;
		xml += audio;
		xml += L"</toast>";

		try
		{
			//auto notifier = ToastNotificationManager::CreateToastNotifier(L"App");
			auto notifier = create_task(ToastNotificationManager::GetDefault()->GetToastNotifierForToastCollectionIdAsync(account)).get();

			if (notifier == nullptr) {
				notifier = ToastNotificationManager::CreateToastNotifier(L"App");
			}

			auto document = ref new XmlDocument();
			document->LoadXml(ref new String(xml.c_str()));

			auto notification = ref new ToastNotification(document);

			if (tag != nullptr)
			{
				notification->Tag = tag;
				notification->RemoteId = tag;
			}

			if (group != nullptr)
			{
				notification->Group = group;

				if (tag != nullptr)
				{
					notification->RemoteId += "_";
				}

				notification->RemoteId += group;
			}

			notifier->Show(notification);
		}
		catch (Exception ^ ex)
		{
			LogError(L"showing toast notification", ex);
		}
	});
}

Windows::Foundation::IAsyncAction^ NotificationTask::ShowIncomingCallToast(String^ caption, String^ message, String^ account, String^ launch, String^ group, String^ picture, String^ date, time_t expires)
{
	return create_async([=]()
	{
		// scenario='incomingCall' keeps the toast on screen with looping call audio. The
		// accept action only opens the app; the call itself is answered on the call screen
		// once TDLib delivers the pending call.
		std::wstring xml = L"<toast scenario='incomingCall' launch='";
		xml += launch->Data();
		xml += L"' displayTimestamp='";
		xml += date->Data();
		xml += L"'><visual><binding template='ToastGeneric'>";

		if (picture != nullptr)
		{
			xml += L"<image placement='appLogoOverride' hint-crop='circle' src='";
			xml += picture->Data();
			xml += L"'/>";
		}

		xml += L"<text><![CDATA[";
		xml += caption->Data();
		xml += L"]]></text><text><![CDATA[";
		xml += message->Data();
		xml += L"]]></text></binding></visual>";
		xml += L"<actions><action activationType='foreground' content='ms-resource:AcceptCall' arguments='";
		xml += launch->Data();
		xml += L"'/><action activationType='system' content='' arguments='dismiss'/></actions>";
		xml += L"<audio src='ms-winsoundevent:Notification.Looping.Call' loop='true'/>";
		xml += L"</toast>";

		try
		{
			auto notifier = create_task(ToastNotificationManager::GetDefault()->GetToastNotifierForToastCollectionIdAsync(account)).get();
			if (notifier == nullptr)
			{
				notifier = ToastNotificationManager::CreateToastNotifier(L"App");
			}

			auto document = ref new XmlDocument();
			document->LoadXml(ref new String(xml.c_str()));

			auto notification = ref new ToastNotification(document);
			notification->Tag = L"phoneCall";
			notification->RemoteId = L"phoneCall";
			if (group != nullptr)
			{
				notification->Group = group;
				notification->RemoteId += "_";
				notification->RemoteId += group;
			}

			notification->ExpirationTime = ref new Box<DateTime>(FromUnixTime(expires));
			notifier->Show(notification);
		}
		catch (Exception ^ ex)
		{
			LogError(L"showing incoming call toast", ex);
		}
	});
}

void NotificationTask::RemoveCallToast(String^ account, String^ group)
{
	auto removed = false;
	try
	{
		auto history = create_task(ToastNotificationManager::GetDefault()->GetHistoryForToastCollectionIdAsync(account)).get();
		if (history != nullptr)
		{
			if (group != nullptr)
			{
				history->Remove(L"phoneCall", group);
			}
			else
			{
				history->Remove(L"phoneCall");
			}
			removed = true;
		}
	}
	catch (Exception ^ ex)
	{
		LogError(L"removing call toast from collection", ex);
	}

	try
	{
		if (group != nullptr)
		{
			ToastNotificationManager::History->Remove(L"phoneCall", group, L"App");
		}
		else
		{
			ToastNotificationManager::History->Remove(L"phoneCall");
		}
		removed = true;
	}
	catch (Exception ^ ex)
	{
		LogError(L"removing call toast", ex);
	}

	LogDiagnostic(L"native.notification.removed", removed ? L"result=success;kind=call" : L"result=failed;kind=call");
}

// TDLib does not resend a ringing call to a session that connects after the call started,
// so the call push is kept for the app to replay through ProcessPushNotification when it
// is cold-launched from the call toast. The file stays in the app's own LocalFolder and is
// consumed or expired by the app; its content is never logged.
// Records which call fields Telegram put in the push (names only, never values) so it is
// visible whether a cold-launched app can rebuild the call from it.
void NotificationTask::LogCallPushShape(JsonObject^ custom, long long skewSeconds)
{
	try
	{
		std::wstring details = L"has_custom=";
		details += custom != nullptr ? L"1" : L"0";
		if (custom != nullptr)
		{
			details += L";call_id=";
			details += custom->HasKey(L"call_id") ? L"1" : L"0";
			details += L";call_ah=";
			details += custom->HasKey(L"call_ah") ? L"1" : L"0";
			details += L";updates=";
			details += custom->HasKey(L"updates") ? L"1" : L"0";
			details += L";keys=";
			bool first = true;
			for (auto it = custom->First(); it->HasCurrent; it->MoveNext())
			{
				std::wstring key = it->Current->Key->Data();
				bool safe = !key.empty() && key.length() <= 32;
				for (auto ch : key)
				{
					if (!(iswalnum(ch) || ch == L'_'))
					{
						safe = false;
						break;
					}
				}

				if (!first)
				{
					details += L",";
				}

				details += safe ? key : L"?";
				first = false;
			}
		}

		details += L";receive_minus_date=";
		details += std::to_wstring(skewSeconds);
		LogDiagnostic(L"native.call.shape", ref new String(details.c_str()));
	}
	catch (Exception ^ ex)
	{
		LogError(L"describing call push", ex);
	}
}

void NotificationTask::SavePendingCallPush(String^ content, String^ session, time_t receivedTime)
{
	try
	{
		std::wstring path = ApplicationData::Current->LocalFolder->Path->Data();
		path += L"\\pending-call-push.json";

		std::wstring wide = content->Data();
		auto length = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.length()), nullptr, 0, nullptr, nullptr);
		std::string utf8(length, '\0');
		WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.length()), &utf8[0], length, nullptr, nullptr);

		{
			std::ofstream file(path, std::ios::trunc | std::ios::binary);
			if (!file.is_open())
			{
				LogDiagnostic(L"native.call.pending", L"result=failed;reason=file_unavailable");
				return;
			}

			file.write(utf8.data(), utf8.size());
		}

		auto values = ApplicationData::Current->LocalSettings->Values;
		values->Insert(L"PendingCallPushSession", session);
		values->Insert(L"PendingCallPushTime", PropertyValue::CreateInt64(static_cast<int64>(receivedTime)));
		LogDiagnostic(L"native.call.pending", L"result=saved");
	}
	catch (Exception ^ ex)
	{
		LogError(L"saving pending call push", ex);
	}
}

void NotificationTask::ClearPendingCallPush()
{
	try
	{
		auto values = ApplicationData::Current->LocalSettings->Values;
		if (values->HasKey(L"PendingCallPushSession"))
		{
			values->Remove(L"PendingCallPushSession");
		}
		if (values->HasKey(L"PendingCallPushTime"))
		{
			values->Remove(L"PendingCallPushTime");
		}

		std::wstring path = ApplicationData::Current->LocalFolder->Path->Data();
		path += L"\\pending-call-push.json";
		DeleteFileW(path.c_str());
		LogDiagnostic(L"native.call.pending", L"result=cleared");
	}
	catch (Exception ^ ex)
	{
		LogError(L"clearing pending call push", ex);
	}
}

//
//void NotificationTask::UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key)
//{
//	auto coordinator = VoipCallCoordinator::GetDefault();
//	create_task(coordinator->ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask")).then([this, coordinator, caption, message, sound, launch, tag, group, picture, date, loc_key](VoipPhoneCallResourceReservationStatus status)
//	{
//		Sleep(1000000);
//		//VoIPCallTask::Current->UpdatePhoneCall(caption, message, sound, launch, tag, group, picture, date, loc_key);
//	});
//}
