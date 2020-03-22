using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;



namespace PresenceBot {
	class Program {
		private static readonly ulong ADMIN_ROLE_ID = 689442668714917899;
		private static readonly ulong GUILD_ID = 689438602228400240;
		private static readonly ulong LOG_TEXT_ID = 690497509138628619;
		private static readonly ulong GENERAL_TEXT_ID = 690498237857136660;


		// Constants
		private static readonly int TOKEN_INDEX = 0;
		private static readonly int SECOND_TO_MILISECOND = 1000;
		private static readonly int MINUTE_TO_SECOND = 60;

		// Member fields
		private DiscordSocketClient discordClient;

		// 
		static ulong presenceMessageID;
		static DateTime presenceMessageTime;
		static Dictionary<ulong, bool> userPresence = null;
		static ulong presencePositiveID;
		static string presenceText;



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

			
			discordClient.ReactionAdded += this.DiscordClient_ReactionAdded;
			discordClient.ReactionRemoved += this.DiscordClient_ReactionRemoved;
			discordClient.UserVoiceStateUpdated += this.DiscordClient_UserVoiceStateUpdated;
			userPresence = new Dictionary<ulong, bool>();
			await discordClient.LoginAsync(Discord.TokenType.Bot, token);
			await discordClient.StartAsync();
			discordClient.Connected += this.DiscordClient_Connected;

			// Run indefinitely
			await Task.Delay(-1);
		}

		
		private async Task DiscordClient_Connected() {
			SocketTextChannel channel;
			do {
				channel = discordClient.GetGuild(GUILD_ID).GetTextChannel(GENERAL_TEXT_ID);
				Thread.Sleep(SECOND_TO_MILISECOND);
			} while (channel == null);

			foreach (var user in discordClient.GetGuild(GUILD_ID).Users) {
				try {
					bool flag = false;
					foreach (var role in user.Roles){
						if (role.Id == ADMIN_ROLE_ID) {
							flag = true;
						}
					}
					
					if (flag || user.IsBot || user.Status == UserStatus.Offline)
					{
						continue;
					}

					userPresence.Add(user.Id, true);
					Console.WriteLine(user.Username);
				} catch {

				}
			}

			var rng = new Random();
			bool newDay = true;
			bool workTime = true;
			bool firstRun = true;
			DateTime now = DateTime.Now;
			DateTime endWork = DateTime.Now;
			DateTime beginWork = DateTime.Now;

			while (true) {
				if (newDay) {
					System.Console.WriteLine("New day");
					beginWork = DateTime.Now.Date + new TimeSpan(9, 0, 0);
					endWork = DateTime.Now.Date + new TimeSpan(18, 0, 0);
					newDay = false;

					if (firstRun) {
						workTime = now > beginWork && now < endWork;
						firstRun = false;
					}
				}

				now = DateTime.Now;
				if (now > beginWork && now < endWork) {
					System.Console.WriteLine("New round");
					int random = rng.Next(5, 55);
					System.Console.WriteLine("Check on: " + random);
					if (random > now.Minute) {
						if (!workTime) {
							await channel.SendMessageAsync("Günaydın @everyone!");
							workTime = true;
						}

						Thread.Sleep((random-now.Minute) * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
						await CheckPresence(channel);
						System.Console.WriteLine("Check");
					} else
					{
						System.Console.WriteLine("Abort");
					}
					now = DateTime.Now;
					int remain = 60 - now.Minute;
					System.Console.WriteLine("Wait end " + remain);
					Thread.Sleep(remain * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
				} else {
					if (workTime) {
						await channel.SendMessageAsync("İyi akşamlar @everyone!");
						workTime = false;
					}

					int remain = 60 - now.Minute;
					System.Console.WriteLine("Sleep " + remain);
					Thread.Sleep(remain * MINUTE_TO_SECOND * SECOND_TO_MILISECOND);
					newDay = now.Day != beginWork.Day;
				}
			}
		}
		

		private async Task CheckPresence(SocketTextChannel channel) {
			Console.WriteLine(111111111);
			StringBuilder builder = new StringBuilder();
			foreach (var user in userPresence) {
				if (!user.Value) {
					string name = channel.Guild.GetUser(user.Key).Nickname ?? channel.Guild.GetUser(user.Key).Username;
					builder.Append(name + "\n");
				}
			}


			Console.WriteLine(22222222);
			userPresence.Clear();
			foreach (var user in discordClient.GetGuild(GUILD_ID).Users) {
				try {
					bool flag = false;
					foreach (var role in user.Roles)
					{
						if (role.Id == ADMIN_ROLE_ID)
						{
							flag = true;
						}
					}

					if (flag || user.IsBot || user.Status == UserStatus.Offline) {
						continue;
					}
					
					userPresence.Add(user.Id, false);
					Console.WriteLine(user.Username);
				} catch {

				}
			}


			Console.WriteLine(333333333);
			if (builder.ToString().Length > 0)
			{
				Console.WriteLine(3.1);
				EmbedBuilder bustEmbed = new EmbedBuilder();
				Console.WriteLine(3.2);
				bustEmbed.Title = "Önceki Yoklamayı Kaçıranlar";
				Console.WriteLine(3.3);
				bustEmbed.Color = Color.DarkRed;
				Console.WriteLine(3.4);
				bustEmbed.Description = builder.ToString();
				Console.WriteLine(3.5);
				bustEmbed.Timestamp = presenceMessageTime;
				Console.WriteLine(3.6);
				await (discordClient.GetChannel(LOG_TEXT_ID) as SocketTextChannel).SendMessageAsync("", false, bustEmbed.Build());
				Console.WriteLine(3.7);
			}
			
			Console.WriteLine(444444444);
			EmbedBuilder checklistEmbed = new EmbedBuilder();
			presenceText = string.Empty;
			checklistEmbed.Title = "Yoklamaya katılanlar";
			checklistEmbed.Color = Color.Green;
			var checkListMsg = await (discordClient.GetChannel(LOG_TEXT_ID) as SocketTextChannel).SendMessageAsync("", false, checklistEmbed.Build());
			presencePositiveID = checkListMsg.Id;


			Console.WriteLine(555555555);
			EmbedBuilder embed = new EmbedBuilder();
			embed.Title = "Yoklama";
			embed.Color = Color.Green;
			embed.Description = "@everyone ?";
			var msg = await channel.SendMessageAsync("", false, embed.Build());
			await msg.AddReactionAsync(new Emoji("👌"));


			Console.WriteLine(66666666666);
			presenceMessageID = msg.Id;
			presenceMessageTime = DateTime.Now;
	
		}
		
		private Task DiscordClient_ReactionRemoved(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
			//throw new System.NotImplementedException();
			return Task.CompletedTask;
		}

		private async Task DiscordClient_ReactionAdded(Discord.Cacheable<Discord.IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
		{
			if (message.Id == presenceMessageID && !reaction.User.Value.IsBot && userPresence.ContainsKey(reaction.User.Value.Id)) {
				if (!userPresence[reaction.User.Value.Id]) {
					userPresence[reaction.User.Value.Id] = true;
					TimeSpan diff = DateTime.Now - presenceMessageTime;

					var positiveMsg = await discordClient.GetGuild(GUILD_ID).GetTextChannel(LOG_TEXT_ID).GetMessageAsync(presencePositiveID);
					
					await (positiveMsg as IUserMessage).ModifyAsync(msg => {
						EmbedBuilder newEmbed = new EmbedBuilder();

						newEmbed.Title = "Yoklamaya katılanlar";
						newEmbed.Color = Color.Green;
						newEmbed.Timestamp = presenceMessageTime;

						var user = discordClient.GetGuild(GUILD_ID).GetUser(reaction.User.Value.Id);
						string username = user.Nickname;
						if (user.Nickname == null)
						{
							username = user.Username;
						}
						presenceText = presenceText + "\n" + username;
						


						if (diff.Minutes > 5) {
							presenceText = presenceText + "   --   **" + diff.Minutes + "dk geç**";
						}

						newEmbed.Description = presenceText;
						msg.Embed = newEmbed.Build();
					});
				}
			}
			
		}

		private Task DiscordClient_UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2) {
			DateTime now = DateTime.Now;
			var embed = new EmbedBuilder();
			embed.Timestamp = now;


			var usr = discordClient.GetGuild(GUILD_ID).GetUser(user.Id);
			string username = usr.Nickname;
			if (usr.Nickname == null)
			{
				username = usr.Username;
			} else if (usr.Nickname == string.Empty)
			{
				username = usr.Username;
			}



			if (state1.VoiceChannel == null) {
				if (state2.VoiceChannel != null) {
					embed.Color = Color.Green;
					embed.Description = username + " kanala **katıldı** (" + (state2.VoiceChannel.Category == null ? "" : state2.VoiceChannel.Category + ".") + state2.VoiceChannel.Name + ")";
					//embed.ThumbnailUrl = user.GetAvatarUrl();
					state2.VoiceChannel.Guild.GetTextChannel(LOG_TEXT_ID).SendMessageAsync("", false, embed.Build());
				} else {
					System.Console.WriteLine("WHAT THE FUCK");
				}
			} else {
				if (state2.VoiceChannel == null) {
					embed.Color = Color.DarkRed;
					embed.Description = username + " **kanaldan çıktı** (" + (state1.VoiceChannel.Category == null ? "" : state1.VoiceChannel.Category + ".") + state1.VoiceChannel.Name + ")";
					//embed.ThumbnailUrl = user.GetAvatarUrl();
					state1.VoiceChannel.Guild.GetTextChannel(LOG_TEXT_ID).SendMessageAsync("", false, embed.Build());
				}

				// Event triggered if user mutes or deafens himself/herself and vice versa as well
				// Ensure that the channel names are different to print a meaningful log
				else if (!state1.VoiceChannel.Name.Equals(state2.VoiceChannel.Name)){
					embed.Description = username + " **kanal değiştirdi** (" +
						(state1.VoiceChannel.Category == null ? "" : state1.VoiceChannel.Category + ".") + state1.VoiceChannel.Name + " **→** " +
						(state2.VoiceChannel.Category == null ? "" : state2.VoiceChannel.Category + ".") + state2.VoiceChannel.Name + ")";
					state2.VoiceChannel.Guild.GetTextChannel(LOG_TEXT_ID).SendMessageAsync("", false, embed.Build());
				}
			}

			return Task.CompletedTask;
		}
	}
}
