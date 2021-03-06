﻿using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using CurrencyBank.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Wolfje.Plugins.Jist;
using Wolfje.Plugins.Jist.Framework;
using Microsoft.Xna.Framework;

namespace CurrencyBank
{
	[ApiVersion(2, 1)]
	public class BankMain : TerrariaPlugin
	{
		public static BankAccountManager Bank { get; set; }

		public static Config Config { get; set; }

		public static IDbConnection Db { get; set; }

		public static BankLog Log { get; private set; }

		internal static string Tag => TShock.Utils.ColorTag("CurrencyBank:", new Color(137, 73, 167));

		public BankMain(Main game) : base(game)
		{
			Order = 2;
		}

		public override string Author => "Enerdy";

		public override string Description => "SBPlanet Economy Bank Module";

		public override string Name => "CurrencyBank";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				AccountHooks.AccountDelete -= OnAccountDelete;
				GeneralHooks.ReloadEvent -= OnReload;
				JistPlugin.JavascriptFunctionsNeeded -= OnJavascriptFunctionsNeeded;
				PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
			}
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			AccountHooks.AccountDelete += OnAccountDelete;
			GeneralHooks.ReloadEvent += OnReload;
			JistPlugin.JavascriptFunctionsNeeded += OnJavascriptFunctionsNeeded;
			PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
		}

		void OnInitialize(EventArgs e)
		{
			#region Config

			string cpath = Path.Combine(TShock.SavePath, "CurrencyBank", "Config.json");
			Config = Config.Read(cpath);

			#endregion

			#region Commands

			TShockAPI.Commands.ChatCommands.Add(new Command(Commands.CBank, "cbank", "currencybank")
			{
				HelpText = "Perform payments and manage bank accounts."
			});

			#endregion

			#region DB

			if (Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
			{
				string[] host = Config.MySqlHost.Split(':');
				Db = new MySqlConnection()
				{
					ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					host[0],
					host.Length == 1 ? "3306" : host[1],
					Config.MySqlDbName,
					Config.MySqlUsername,
					Config.MySqlPassword)
				};
			}
			else if (Config.StorageType.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
				Db = new SqliteConnection(String.Format("uri=file://{0},Version=3",
					Path.Combine(TShock.SavePath, "CurrencyBank", "Database.sqlite")));
			else
				throw new InvalidOperationException("Invalid storage type!");

			#endregion

			Bank = new BankAccountManager(Db);
			Log = new BankLog(Path.Combine(TShock.SavePath, "CurrencyBank", "Logs", BankLog.GetLogName()));
		}

		async void OnAccountDelete(AccountDeleteEventArgs e)
		{
			if (await Bank.DelAsync(e.User.Name))
				TShock.Log.ConsoleInfo($"CurrencyBank: Deleted bank account for \"{e.User.Name}\".");
		}

		void OnJavascriptFunctionsNeeded(object sender, JavascriptFunctionsNeededEventArgs e)
		{
			JistFunctions functions = new JistFunctions();
			e.Engine.CreateScriptFunctions(functions.GetType(), functions);
		}

		async void OnPlayerPostLogin(PlayerPostLoginEventArgs e)
		{
			BankAccount account;

			if ((account = await Bank.GetAsync(e.Player.User.Name)) == null && e.Player.Group.HasPermission(Permissions.Permit))
			{
				BankAccount newAccount = await BankAccount.Create(e.Player.User.Name);
				if (newAccount == null || !(await Bank.AddAsync(newAccount)))
					TShock.Log.ConsoleError($"CurrencyBank: Unable to create bank account for \"{e.Player.User.Name}\".");
				else
					TShock.Log.ConsoleInfo($"CurrencyBank: Bank account created for \"{e.Player.User.Name}\".");
			}
		}

		void OnReload(ReloadEventArgs e)
		{
			string cpath = Path.Combine(TShock.SavePath, "CurrencyBank", "Config.json");
			Config = Config.Read(cpath);
		}

		public static string FormatMoney(long money)
		{
			var sb = new StringBuilder();
			if (Config.UseShortName)
				sb.Append(Config.CurrencyNameShort);

			sb.Append(money);

			if (!Config.UseShortName)
			{
				sb.Append(" ");
				if (money != 1)
					sb.Append(Config.CurrencyNamePlural);
				else
					sb.Append(Config.CurrencyName);
			}

			return sb.ToString();
		}
	}
}
