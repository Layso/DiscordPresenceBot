using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;



namespace PresenceBot {
	class Program {
		// Discord ID variables, set for your own channel
		private static readonly ulong ADMIN_ROLE_ID = 689442668714917899;
		private static readonly ulong GUILD_ID = 689438602228400240;
		private static readonly ulong LOG_TEXT_ID = 690497509138628619;
		private static readonly ulong GENERAL_TEXT_ID = 690498237857136660;
		private static readonly ulong AFK_VOICE_ID = 690093162710040576;

		// Work hour definitions
		private static readonly int ACCEPTED_DELAY_MINUTES = 5;
		private static readonly TimeSpan WORK_START_TIME = new TimeSpan(9, 0, 0);
		private static readonly TimeSpan WORK_END_TIME = new TimeSpan(18, 0, 0);
		private static readonly TimeSpan LUNCH_START_TIME = new TimeSpan(12, 0, 0);
		private static readonly TimeSpan LUNCH_END_TIME = new TimeSpan(13, 30, 0);

		// Constants
		private static readonly int TOKEN_INDEX = 0;
		private static readonly int SECOND_TO_MILISECOND = 1000;
		private static readonly int MINUTE_TO_SECOND = 60;

		// Member fields
		private DiscordSocketClient discordClient;

		// Static variables to use between threads (not sure if Tasks are threads and static needed in C# but using anyways..)
		static ulong reactionCheckMessageID = 0;
		static ulong reactionResultMessageID = 0;
		static DateTime reactionCheckMessageTime;
		static Dictionary<ulong, bool> muteList = null;
		static Dictionary<ulong, bool> userPresence = null;
		static Dictionary<ulong, DateTime> userEndTimes = null;
		static Dictionary<ulong, DateTime> userStartTimes = null;



		static void Main(string[] args) {
			if (args.Length <= TOKEN_INDEX) {
				System.Console.WriteLine("Please provide bot token as command line argument");
				System.Console.ReadLine();
			} else {
				new Program().StartAsync(args[TOKEN_INDEX]).GetAwaiter().GetResult();
			}
		}



		private async Task StartAsync(string token) {
			discordClient = new DiscordSocketClient();
			muteList = new Dictionary<ulong, bool>();
			userPresence = new Dictionary<ulong, bool>();
			userEndTimes = new Dictionary<ulong, DateTime>();
			userStartTimes = new Dictionary<ulong, DateTime>();

			
			discordClient.Log += Log;
			discordClient.Connected += this.DiscordClient_Connected;
			discordClient.ReactionAdded += this.DiscordClient_ReactionAdded;
			discordClient.UserVoiceStateUpdated += this.DiscordClient_UserVoiceStateUpdated;

			await discordClient.LoginAsync(Discord.TokenType.Bot, token);
			await discordClient.StartAsync();

			// Run indefinitely
			await Task.Delay(-1);
		}


		
		private async Task DiscordClient_Connected() {
			var rng = new Random();
			bool newDay = true;
			bool workTime = true;
			bool firstRun = true;
			DateTime now = DateTime.Now;
			DateTime beginWork = DateTime.Now;
			DateTime endWork = DateTime.Now;
			DateTime beginLunch = DateTime.Now;
			DateTime endLunch = DateTime.Now;
			SocketTextChannel generalChannel = GetTextChannel(GENERAL_TEXT_ID);

			/* Not sure if needed */
			while (generalChannel == null) {
				Console.WriteLine(DateTime.Now + "   " + "generalChannel is null");
				Thread.Sleep(SECOND_TO_MILISECOND);
				generalChannel = GetTextChannel(GENERAL_TEXT_ID);
			} /* Test and remove if not needed */
			/*	It's actually needed. I have no idea why but trying to reach the text channel returns null everytime,
				for the first immediate call before the while. So I guess I'm keeping this then. Please enlighten me 
				if you know why we can't get text channel uppon connection */


			// Construct presence list for online users, start with state true to prevent listing any users on first presence check
			ConstructPresenceList(true);

			
			// Run indefinitely
			while (true) {
				// If new day detected, set working hours
				if (newDay) {
					userStartTimes.Clear();
					userEndTimes.Clear();
					userPresence.Clear();
					beginWork = DateTime.Now.Date + WORK_START_TIME;
					endWork = DateTime.Now.Date + WORK_END_TIME;
					beginLunch = DateTime.Now.Date + LUNCH_START_TIME;
					endLunch = DateTime.Now.Date + LUNCH_END_TIME;
					newDay = false;

					Console.WriteLine("--------------- NEW DAY -----------------");
					Console.WriteLine(DateTime.Now + "   " + "Work : " + beginWork + " - " + endWork);
					Console.WriteLine(DateTime.Now + "   " + "Lunch: " + beginLunch + " - " + endLunch);
					Console.WriteLine("---------------         -----------------");
					
					// Bot may restart both during or outside work hours, check again to see if it's work time on each start
					if (firstRun) {
						workTime = now > beginWork && now < endWork;
						firstRun = false;
					}
				}

				// Get current time to make all calculations from same time
				now = DateTime.Now;

				// If in working hours
				if (now > beginWork && now < endWork) {
					int checkTime = rng.Next(15, 45);
					Log("RNG = " + checkTime);

					// If it's lunch time sleep for the duration of lunch. Generate specific check time for this time
					if (now > beginLunch && now < endLunch) {
						Log("Sending Bon Appetite message");
						await generalChannel.SendMessageAsync("Afiyet olsun " + GetEveryoneRole().Mention + "!");       // Bon appetite message
						Log("Lunch Sleep for " + (int)((endLunch - now).TotalMinutes) + "mins");
						Thread.Sleep(endLunch - now);
						now = DateTime.Now;
						checkTime = rng.Next(35, 55);
						Log("RNG = " + checkTime);
					}

					// Add a time check in case time has already passed (possible if bot restarted during work hours)
					if (checkTime > now.Minute) {
						if (!workTime) {
							Log("Sending Bonjour message");
							await generalChannel.SendMessageAsync("Günaydın " + GetEveryoneRole().Mention + "!");		 // Good morning message
							workTime = true;
						}

						// Wait until time and then check presence
						Log("Sleep until time check - " + (checkTime - now.Minute) + "mins");
						Thread.Sleep((checkTime - now.Minute) * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);

						Log("Calling check function");
						await CheckPresence(DateTime.Now);
					} else {
						Log("Abort check");
					}

					// Wait until the end of the hour to determine next check time
					int remain = 60 - DateTime.Now.Minute;
					Log("Sleep until next hour - " + remain + "mins");
					Thread.Sleep(remain * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
				}
				
				// If not in working hours
				else {
					// End of work hours
					if (workTime) {
						Log("Finishing up the day");
						await generalChannel.SendMessageAsync((now.DayOfWeek == DayOfWeek.Friday ? "İyi tatiller!" : "İyi akşamlar ") + GetEveryoneRole().Mention + "!");        // Good evening/weekend message
						await FinalizePreviousCheck();
						await ZReport();
						reactionCheckMessageID = 0;
						reactionResultMessageID = 0;
						workTime = false;
					}

					// Sleep until next hour to check if new day has started
					Log("Not in working hours, sleep 1 hour");
					int remain = 60 - DateTime.Now.Minute;
					Thread.Sleep(remain * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
					newDay = DateTime.Now.Day != beginWork.Day;
				}
			}
		}
		

		private async Task CheckPresence(DateTime time) {
			try {
				// Bust unattended people by adding them to the end of the list, skip this step if there isn't any previous message
				if (reactionResultMessageID != 0) {
					await FinalizePreviousCheck();
				} else {
					Log("ERROR::ReactionResultMessageId == 0");
				}

				// Clearn and re-construct presence list,
				ConstructPresenceList(false);

				// Post new reaction result message to log channel
				SocketTextChannel logChannel = GetTextChannel(LOG_TEXT_ID);
				if (logChannel == null) {
					Log("ERROR::LogChannel == null");
				}

				Log("Create new log message");
				EmbedBuilder logEmbed = new EmbedBuilder();
				logEmbed.Title = "Aktif Yoklama";															// Embed title ("Active Attendance")
				logEmbed.Color = Color.Green;
				logEmbed.Timestamp = time;
				logEmbed.Description = "**Katılanlar**";                                                    // SubTitle ("Attended People"), bold
				RestUserMessage newResultMessage = await logChannel.SendMessageAsync("", false, logEmbed.Build());
				Log("New log message sent");

				if (newResultMessage == null) {
					Log("ERROR::NewResultMessage == null");
				}

				reactionResultMessageID = newResultMessage.Id;
				// Post new reaction message to general channel and react to it
				SocketTextChannel generalChannel = GetTextChannel(GENERAL_TEXT_ID);
				if (generalChannel == null) {
					Log("ERROR::GeneralChannel == null");
				}

				Log("Create new reaction message");
				EmbedBuilder embedBuilder = new EmbedBuilder();
				embedBuilder.Title = "Yoklama";                                                             // Title ("Attandance")
				embedBuilder.Color = Color.Green;
				embedBuilder.Timestamp = time;
				RestUserMessage newReactionMessge = await generalChannel.SendMessageAsync(GetEveryoneRole().Mention, false, embedBuilder.Build());
				Log("New reaction message sent");

				if (newReactionMessge == null) {
					Log("ERROR::NewRactionMessage == null");
				}

				reactionCheckMessageID = newReactionMessge.Id;
				reactionCheckMessageTime = time;

				Log("Reacting message");
				await newReactionMessge.AddReactionAsync(new Emoji("👌"));
				Log("Reacted message");

				
				Log("-------------------------------------------");
				foreach(SocketGuildUser user in GetGuildUsers()) {
					Log("ARR::User -> " + user.Username);
					if (!user.IsBot && !IsAdmin(user.Id) && !muteList.ContainsKey(user.Id)) {
						await user.SendMessageAsync(time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2") + " saatindeki yoklamayı kaçırma! " + GetTextChannel(GENERAL_TEXT_ID).Mention);
						Log("ARR::User -> " + user.Username + " sent");
					}
				}
				Log("-------------------------------------------");
			} catch (Exception e) {
				Console.WriteLine(DateTime.Now + "   " + "Exception: " + e.Message);
			}
		}


		private async Task FinalizePreviousCheck() {
			if (reactionResultMessageID > 0 && reactionCheckMessageID > 0) {
				string bustMessageText = GetBustedList();
				IUserMessage oldResultMessage = await GetMessage(LOG_TEXT_ID, reactionResultMessageID);
				IUserMessage oldReactionMessage = await GetMessage(GENERAL_TEXT_ID, reactionCheckMessageID);

				if (oldResultMessage == null) {
					Log("ERROR::ResultMessage == null (" + reactionResultMessageID + ")");
				}

				if (oldReactionMessage == null) {
					Log("ERROR::ReactionMessage == null (" + reactionCheckMessageID + ")");
				}

				// Get old log message embed to update the existing text
				Embed oldResultEmbed = null;
				foreach (Embed embed in oldResultMessage.Embeds) {
					oldResultEmbed = embed;
				}

				if (oldResultEmbed == null) {
					Log("ERROR::ResultEmbed == null");
				}

				// Prepare new embed
				Log("Prepare result embed");
				EmbedBuilder updatedResultEmbed = new EmbedBuilder();
				updatedResultEmbed.Title = "Biten Yoklama";                                                 // Title ("Expired Attendance")
				updatedResultEmbed.Color = Color.LightOrange;
				updatedResultEmbed.Timestamp = reactionCheckMessageTime;
				updatedResultEmbed.Description = oldResultEmbed.Description + bustMessageText;
				await oldResultMessage.ModifyAsync(msg => msg.Embed = updatedResultEmbed.Build());
				Log("Result message modified");

				// Also update the title and color of reaction message
				Log("Prepare reaction embed");
				EmbedBuilder updatedReactionEmbed = new EmbedBuilder();
				updatedReactionEmbed.Title = "Biten Yoklama";
				updatedReactionEmbed.Color = Color.LightOrange;
				updatedReactionEmbed.Timestamp = reactionCheckMessageTime;
				await oldReactionMessage.ModifyAsync(msg => msg.Embed = updatedReactionEmbed.Build());
				Log("Reaction message modified");
			} else {
				Log("No previous check");
			}
		}


		private async Task ZReport() {
			Thread.Sleep(10 * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
			StringBuilder builder = new StringBuilder();
			foreach (var pair in userStartTimes) {
				SocketGuildUser user = GetUser(pair.Key);
				builder.Append("\n");
				builder.Append(user.Nickname ?? user.Username);
				builder.Append(" " + pair.Value.Hour.ToString("D2") + ":" + pair.Value.Minute.ToString("D2"));
				builder.Append(" - ");

				if (user.VoiceChannel != null && user.VoiceChannel.Id != AFK_VOICE_ID) {
					builder.Append("Devam");
				} else if (userEndTimes.ContainsKey(pair.Key)) {
					builder.Append(userEndTimes[user.Id].Hour.ToString("D2") + ":" + userEndTimes[user.Id].Minute.ToString("D2"));
				} else {
					builder.Append("?");
				}
			}

			foreach (SocketGuildUser user in GetGuildUsers()) {
				if (!user.IsBot && !IsAdmin(user.Id) && !userStartTimes.ContainsKey(user.Id)) {
					builder.Append("\n");
					builder.Append(user.Nickname ?? user.Username);
					builder.Append(" - ");
					builder.Append("Katılmadı");
				}
			}

			EmbedBuilder embed = new EmbedBuilder();
			embed.Title = "Gün Sonu";
			embed.Color = Color.Green;
			embed.Description = builder.ToString();
			embed.Timestamp = DateTime.Now;

			await GetTextChannel(LOG_TEXT_ID).SendMessageAsync("", false, embed.Build());
		}


		private async Task DiscordClient_ReactionAdded(Discord.Cacheable<Discord.IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction) {
			try {
				Log("Reaction coming: " + reaction.User.Value.Username);
				/*	Validity check to continue:
					- Reacton must be added to the control message sent by this bot
					- User must be a valid user (not a bot, not an admin)
					- User should be online when reaction check message has been sent 
					- User shouldn't be reacted to the message before */
				if (message.Id == reactionCheckMessageID && !reaction.User.Value.IsBot && !IsAdmin(reaction.UserId) && !userPresence[reaction.UserId]) {
					Log("Adding to list: " + reaction.User.Value.Username);
					userPresence[reaction.UserId] = true;
					Log("Set to true: " + reaction.User.Value.Username);
					// Prepare the result line for user
					SocketGuildUser user = GetUser(reaction.UserId);
					TimeSpan timeDiff = DateTime.Now - reactionCheckMessageTime;

					string newLine = string.Format("\n{0, -20} {1}", user.Nickname ?? user.Username, timeDiff.Minutes > ACCEPTED_DELAY_MINUTES ? "+ " + timeDiff.Minutes + " dk" : string.Empty);

					// Add user to result list message
					IUserMessage resultMessage = await GetMessage(LOG_TEXT_ID, reactionResultMessageID);
					Embed oldEmbed = null;
					EmbedBuilder updatedEmbed = new EmbedBuilder();
					foreach(Embed embed in resultMessage.Embeds) {
						oldEmbed = embed;
					}

					if (oldEmbed == null) {
						Log("ERROR::OldEmbed == null");
					}

					Log("Prepare new embed");
					updatedEmbed.Title = oldEmbed.Title;
					updatedEmbed.Color = oldEmbed.Color;
					updatedEmbed.Timestamp = oldEmbed.Timestamp;
					updatedEmbed.Description = oldEmbed.Description + newLine;
					await resultMessage.ModifyAsync(msg => msg.Embed = updatedEmbed.Build());
					Log("New embed added");
				} else {
					StringBuilder builder = new StringBuilder();
					builder.Append(reaction.User.Value.Username + " reaction denied. Reason:");

					if (message.Id != reactionCheckMessageID) {
						builder.Append("\n - Incorrect message");
					}

					if (reaction.User.Value.IsBot) {
						builder.Append("\n - User is bot");
					}

					if (IsAdmin(reaction.UserId)) {
						builder.Append("\n - User is admin");
					}

					if (userPresence[reaction.UserId]) {
						builder.Append("\n - Already reacted");
					}

					Log(builder.ToString());
				}
			} catch (Exception e) {
				Log("EXCEPTION_HANDLED: " + e.Message);
			}
		}


		private Task DiscordClient_UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2) {
			// Exclude admins and bots
			if (!user.IsBot && !IsAdmin(user.Id)) {
				// Join to a voice channel
				if (state1.VoiceChannel == null && state2.VoiceChannel != null) {
					if (!userStartTimes.ContainsKey(user.Id)) {
						userStartTimes.Add(user.Id, DateTime.Now);
					}
				}

				// Leave a voice channel
				else if (state1.VoiceChannel != null && state2.VoiceChannel == null) {
					if (!userEndTimes.ContainsKey(user.Id)) {
						userEndTimes.Add(user.Id, DateTime.Now);
					} else {
						userEndTimes[user.Id] = DateTime.Now;
					}
				}

				// Switch channels, check if user stepped into the AFK channel
				else if (state1.VoiceChannel != null && state2.VoiceChannel != null && state2.VoiceChannel.Id == AFK_VOICE_ID) {
					if (!userEndTimes.ContainsKey(user.Id)) {
						userEndTimes.Add(user.Id, DateTime.Now);
					} else {
						userEndTimes[user.Id] = DateTime.Now;
					}
				}
			}


			return Task.CompletedTask;
		}


		// Helper functions
		private void Log(string message) {
			Console.WriteLine(DateTime.Now + "   " + message);
		}

		private Task Log(LogMessage message) {
			Console.WriteLine("API log - " + message.ToString());
			return Task.CompletedTask;
		}

		private SocketTextChannel GetTextChannel(ulong id) {
			return discordClient.GetGuild(GUILD_ID).GetTextChannel(id);
		}

		private SocketVoiceChannel GetVoiceChannel(ulong id) {
			return discordClient.GetGuild(GUILD_ID).GetVoiceChannel(id);
		}

		private SocketGuildUser GetUser(ulong id) {
			return discordClient.GetGuild(GUILD_ID).GetUser(id);
		}

		private SocketGuild GetGuild() {
			return discordClient.GetGuild(GUILD_ID);
		}

		private SocketRole GetEveryoneRole() {
			return discordClient.GetGuild(GUILD_ID).EveryoneRole;
		}

		private async Task<IUserMessage> GetMessage(ulong channel, ulong message) {
			return await discordClient.GetGuild(GUILD_ID).GetTextChannel(channel).GetMessageAsync(message) as IUserMessage;
		}

		private bool IsAdmin(ulong id) {
			foreach (SocketRole role in discordClient.GetGuild(GUILD_ID).GetUser(id).Roles) {
				if (role.Id == ADMIN_ROLE_ID) {
					return true;
				}
			}

			return false;
		}

		private string GetBustedList() {
			StringBuilder builder = new StringBuilder();
			string title = "\n\n**Katılmayanlar**\n";					// Title ("Unattended People"), bold, on new line

			foreach (var pair in userPresence) {
				if (!userPresence[pair.Key]) {
					SocketGuildUser user = GetUser(pair.Key);
					builder.Append(user.Nickname ?? user.Username);		// Nickname if exists, else username
					builder.Append("\n");
				}
			}

			return title + (builder.ToString().Length > 0 ? builder.ToString() : "*Yoklamaya herkes katıldı*");	// Either list of unattended people or a message of ("Everyone is present")
		}

		private IReadOnlyCollection<SocketGuildUser> GetGuildUsers() {
			return discordClient.GetGuild(GUILD_ID).Users;
		}

		// Clear presence list and reconstruct with online people for who newly become online/offline
		private void ConstructPresenceList(bool initialState) {
			userPresence.Clear();
			foreach (SocketGuildUser user in GetGuildUsers()) {
				if (!user.IsBot && !IsAdmin(user.Id)) {
					userPresence.Add(user.Id, initialState);
				}
			}
		}
	}
}
