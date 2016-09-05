using CoreTweet;
using CoreTweet.Streaming;
using OktmBot.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using static CoreTweet.OAuth;

namespace OktmBot
{
	class Program
	{
		#region Classes
		private static Tokens tokens;
		private static OAuthSession session;
		private static UserResponse userInfo;
		#endregion

		#region APIKeys
		private const string ConsumerKey = "";
		private const string ConsumerSecret = "";
		#endregion

		/// <summary>
		/// メイン
		/// </summary>
		/// <param name="args"></param>
		private static void Main(string[] args)
		{
			var AccessToken = Settings.Default.AccessToken;
			var AccessTokenSecret = Settings.Default.AccessTokenSecret;
			var UserID = Settings.Default.UserID;
			var ScreenName = Settings.Default.ScreenName;

			/*認証済み*/
			if (AccessToken != null && AccessTokenSecret != null && UserID != 0 && ScreenName != null)
			{
				tokens = Tokens.Create(ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret);

				if (tokens != null)
				{
					WriteLineWithTime("Login Success");

					userInfo = tokens.Account.VerifyCredentials();

					WriteAccountInfomations(userInfo);
				}

				else
				{
					WriteLineWithTime("Login Failures", ConsoleKind.Failure);
					return;
				}
			}

			/*初回起動 or 未認証*/
			else
			{
				session = OAuth.Authorize(ConsumerKey, ConsumerSecret);

				Process.Start(session.AuthorizeUri.AbsoluteUri);

				WriteLineWithTime("Enter the pin showed on your browser.");
				Console.Write(">>>");

				if (Authorize(Console.ReadLine()))
				{
					WriteLineWithTime("Login Success");

					userInfo = tokens.Account.VerifyCredentials();

					WriteAccountInfomations(userInfo);
				}

				else
				{
					WriteLineWithTime("Login Failures", ConsoleKind.Failure);
					return;
				}
			}

			var stream = tokens.Streaming.UserAsObservable().Publish();

			/*ツイート取得イベント*/
			stream.OfType<StatusMessage>().Subscribe(x =>
			{
				var status = x.Status;

				if (status.InReplyToScreenName == userInfo.ScreenName && status.InReplyToUserId == userInfo.Id)
				{
					WriteLineWithTime("Received a new tweet just now.", ConsoleKind.Notification);

					try
					{
						var statusText = status.Text;
						var regex = new Regex("@[a-zA-Z0-9_]*");
						var matches = regex.Matches(statusText).Cast<Match>();

						/*ツイート内から正規表現@*****を削除*/
						foreach (var m in matches)
						{
							if (m.Success)
							{
								statusText = statusText.Replace(m.Value, "");
							}
						}
						
						var text = "";

						/*逆さ言葉作成*/
						foreach (var t in statusText.Reverse())
						{
							text += t;
						}

						/*ツイート*/
						var res = tokens.Statuses.Update(new Dictionary<string, object>()
						{
							{"status",  "@" + status.User.ScreenName + " " + text},
							{"in_reply_to_status_id", status.Id}
						});

						WriteLineWithTime("Succeed to send a tweet.", ConsoleKind.Success);
						WriteLineWithTime("=> " + res.Text, ConsoleKind.Result);
					}

					catch (Exception ex)
					{
						WriteLineWithTime("Failed to send a tweet.", ConsoleKind.Failure);
						WriteLineWithTime(ex.Message, ConsoleKind.ErrorMessage);
					}
				}
			});

			/*ダイレクトメッセージ取得イベント*/
			stream.OfType<DirectMessageMessage>().Subscribe(x =>
			{
				var dm = x.DirectMessage;

				WriteLineWithTime("Received a new directmessage just now.");

				try
				{
					var text = "";
					var dmText = dm.Text;

					/*逆さ言葉作成*/
					foreach (var t in dmText.Reverse())
					{
						text += t;
					}

					/*ツイート*/
					var res = tokens.DirectMessages.New(new Dictionary<string, object>()
					{
						{"text", text},
						{"user_id", dm.Sender.Id},
					});

					WriteLineWithTime("Succeed to send a directmessage.", ConsoleKind.Success);
					WriteLineWithTime("=> At @" + dm.Sender.ScreenName + ": " + res.Text, ConsoleKind.Result);
				}

				catch (Exception ex)
				{
					WriteLineWithTime("Failed to send a directmessage.", ConsoleKind.Failure);
					WriteLineWithTime(ex.Message, ConsoleKind.ErrorMessage);
				}
			});

			/*イベント取得イベント*/
			stream.OfType<EventMessage>().Subscribe(x =>
			{
				switch (x.Event)
				{
					case EventCode.Follow:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has followed " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.Notification);
							break;
						}

					case EventCode.Unfollow:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has unfollowed " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.BadNotification);
							break;
						}

					case EventCode.Favorite:
						{
							var source = x.Source;
							var target = x.Target;
							var tweet = x.TargetStatus;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has favorited " + target.Name + "(@" + target.ScreenName + ")'s tweet. (" + tweet.Text + "/ID: " + tweet.Id + ")", ConsoleKind.Notification);
							break;
						}

					case EventCode.Block:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has blocked " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.BadNotification);
							break;
						}

					case EventCode.Unblock:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has unblocked " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.Notification);
							break;
						}

					case EventCode.Mute:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has muted " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.BadNotification);
							break;
						}

					case EventCode.Unmute:
						{
							var source = x.Source;
							var target = x.Target;

							WriteLineWithTime(source.Name + "(@" + source.ScreenName + ") has unmuted " + target.Name + "(@" + target.ScreenName + ")", ConsoleKind.Notification);
							break;
						}
				}
			});

			/*接続*/
			var disposable = stream.Connect();

			/*キーが押されたら終了*/
			if (Console.ReadKey().KeyChar != ' ')
			{
				disposable.Dispose();
			}
		}

		/// <summary>
		/// トークン取得と保存
		/// </summary>
		/// <param name="pin">PINコード</param>
		/// <returns></returns>
		private static bool Authorize(string pin)
		{
			tokens = OAuth.GetTokens(session, pin);

			if (tokens == null)
			{
				return false;
			}

			Settings.Default.AccessToken = tokens.AccessToken;
			Settings.Default.AccessTokenSecret = tokens.AccessTokenSecret;
			Settings.Default.UserID = tokens.UserId;
			Settings.Default.ScreenName = tokens.ScreenName;
			Settings.Default.Save();
			return true;
		}

		/// <summary>
		/// 時間と色付きで文字出力
		/// </summary>
		/// <param name="text"></param>
		/// <param name="kind"></param>
		private static void WriteLineWithTime(string text, ConsoleKind kind = ConsoleKind.Normal)
		{
			var CharColor = new ConsoleColor();

			switch (kind)
			{
				case ConsoleKind.BadNotification:
					{
						CharColor = ConsoleColor.Yellow;
						break;
					}

				case ConsoleKind.ErrorMessage:
				case ConsoleKind.Failure:
					{
						CharColor = ConsoleColor.Red;
						break;
					}

				case ConsoleKind.Normal:
					{
						CharColor = ConsoleColor.Gray;
						break;
					}

				case ConsoleKind.Notification:
					{
						CharColor = ConsoleColor.Green;
						break;
					}

				case ConsoleKind.Result:
					{
						CharColor = ConsoleColor.Blue;
						break;
					}

				case ConsoleKind.Success:
					{
						CharColor = ConsoleColor.Cyan;
						break;
					}
			}

			Console.ForegroundColor = CharColor;
			Console.WriteLine("[" + DateTime.Now.ToString("MM/dd HH:mm:ss") + "] " + text);
		}

		/// <summary>
		/// 重複処理
		/// </summary>
		/// <param name="userInfo"></param>
		private static void WriteAccountInfomations(UserResponse userInfo)
		{
			WriteLineWithTime("<Account Informations>");
			WriteLineWithTime("DisplayName: " + userInfo.Name);
			WriteLineWithTime("ScreenName: @" + userInfo.ScreenName);
			WriteLineWithTime("UserID: " + userInfo.Id);
			WriteLineWithTime("Description: " + userInfo.Description);
			WriteLineWithTime("Tweets: " + userInfo.StatusesCount);
			WriteLineWithTime("Following: " + userInfo.FriendsCount);
			WriteLineWithTime("Followers: " + userInfo.FollowersCount);
			WriteLineWithTime("Listed: " + userInfo.ListedCount);
			WriteLineWithTime("Favorites: " + userInfo.FavouritesCount);
		}

		/// <summary>
		/// 報告の種類
		/// </summary>
		private enum ConsoleKind
		{
			BadNotification,
			ErrorMessage,
			Failure,
			Normal,
			Notification,
			Result,
			Success
		}
	}
}