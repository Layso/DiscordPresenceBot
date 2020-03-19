using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;



namespace PresenceBot {
	class Program {
		// Constants
		private static readonly int TOKEN_INDEX = 0;
		private static readonly int SECOND_TO_MILISECOND = 1000;
		private static readonly int MINUTE_TO_SECOND = 60;

		// Member fields
		private DiscordSocketClient discordClient;
		internal static ulong presenceMessageID;



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
			await discordClient.LoginAsync(Discord.TokenType.Bot, token);
			await discordClient.StartAsync();
			discordClient.Connected += this.DiscordClient_Connected;

			// Run indefinitely
			await Task.Delay(-1);
		}


		/*
		 var channel = discordClient.GetGuild(317960314106675210).GetTextChannel(317960314106675210);
			while (true)
			{
				System.Console.WriteLine(3);
				if (channel != null)
				{
					System.Console.WriteLine(4);
					channel.SendMessageAsync("Hibernate");
					System.Console.WriteLine(5);
					Thread.Sleep(10000);
					System.Console.WriteLine(6);
				}
			}
			 */

		private Task DiscordClient_Connected() {
			SocketTextChannel channel;
			do {
				channel = discordClient.GetGuild(317960314106675210).GetTextChannel(317960314106675210);
				Thread.Sleep(SECOND_TO_MILISECOND);
			} while (channel == null);
			

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
					beginWork = DateTime.Now.Date + new TimeSpan(3, 43, 0);
					endWork = DateTime.Now.Date + new TimeSpan(3, 46, 0);
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
					if (random > now.Second) {
						if (!workTime) {
							channel.SendMessageAsync("Günaydın @everyone!");
							workTime = true;
						}

						Thread.Sleep(random  * SECOND_TO_MILISECOND);
						CheckPresence(channel).Start();
						System.Console.WriteLine("Check");
					} else
					{
						System.Console.WriteLine("Abort");
					}
					now = DateTime.Now;
					int remain = 60 - now.Second;
					System.Console.WriteLine("Wait end " + remain);
					Thread.Sleep(remain  * SECOND_TO_MILISECOND);
				} else {
					if (workTime) {
						channel.SendMessageAsync("İyi akşamlar @everyone!");
						workTime = false;
					}

					int remain = 60 - now.Second;
					System.Console.WriteLine("Sleep " + remain);
					Thread.Sleep(remain  * SECOND_TO_MILISECOND);
					newDay = now.Day != beginWork.Day;
				}
			}
		}

		private async Task CheckPresence(SocketTextChannel channel) {
			var embed = new EmbedBuilder();
			embed.Description = "@everyone Yoklama";
			var msg = await channel.SendMessageAsync("", false, embed.Build());
			await msg.AddReactionAsync(new Emoji("👌"));
			presenceMessageID = msg.Id;
			System.Console.WriteLine(presenceMessageID);
		}

		private Task DiscordClient_ReactionRemoved(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
			//throw new System.NotImplementedException();
			return Task.CompletedTask;
		}

		private Task DiscordClient_ReactionAdded(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
			System.Console.WriteLine(arg1.Id);
			return Task.CompletedTask;
		}

		private Task DiscordClient_UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2) {
			DateTime now = DateTime.Now;
			var embed = new EmbedBuilder();
			embed.Timestamp = now;

			if (state1.VoiceChannel == null) {
				if (state2.VoiceChannel != null) {
					embed.Color = Color.Green;
					embed.Description = user.Username + " has **joined** the server (" + (state2.VoiceChannel.Category == null ? "" : state2.VoiceChannel.Name + ".") + state2.VoiceChannel.Name + ")";
					//embed.ThumbnailUrl = user.GetAvatarUrl();
					state2.VoiceChannel.Guild.GetTextChannel(689950317567410209).SendMessageAsync("", false, embed.Build());
				} else {
					System.Console.WriteLine("WHAT THE FUCK");
				}
			} else {
				if (state2.VoiceChannel == null) {
					embed.Color = Color.DarkRed;
					embed.Description = user.Username + " has **left** the server (" + (state1.VoiceChannel.Category == null ? "" : state1.VoiceChannel.Name + ".") + state1.VoiceChannel.Name + ")";
					//embed.ThumbnailUrl = user.GetAvatarUrl();
					state1.VoiceChannel.Guild.GetTextChannel(689950317567410209).SendMessageAsync("", false, embed.Build());
				}

				// Event triggered if user mutes or deafens himself/herself and vice versa as well
				// Ensure that the channel names are different to print a meaningful log
				else if (!state1.VoiceChannel.Name.Equals(state2.VoiceChannel.Name)){
					embed.Description = user.Username + " switched channels (" +
						(state1.VoiceChannel.Category == null ? "" : state1.VoiceChannel.Category + ".") + state1.VoiceChannel.Name + " **→** " +
						(state2.VoiceChannel.Category == null ? "" : state2.VoiceChannel.Category + ".") + state2.VoiceChannel.Name + ")";
					state2.VoiceChannel.Guild.GetTextChannel(689950317567410209).SendMessageAsync("", false, embed.Build());
				}
			}

			return Task.CompletedTask;
		}
	}
}
