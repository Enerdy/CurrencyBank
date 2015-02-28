﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CurrencyBank.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace CurrencyBank
{
	[ApiVersion(1, 16)]
	public class BankMain : TerrariaPlugin
	{
		public static BankAccountManager Bank { get; set; }

		public static Config Config { get; set; }

		public static IDbConnection Db { get; set; }

		public BankMain(Main game)
			: base(game)
		{

		}

		public override string Author
		{
			get { return "Enerdy"; }
		}

		public override string Description
		{
			get { return "SBPlanet Economy Bank Module"; }
		}

		public override string Name
		{
			get { return "CurrencyBank"; }
		}

		public override Version Version
		{
			get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				GeneralHooks.ReloadEvent -= OnReload;
				PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
			}
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			GeneralHooks.ReloadEvent += OnReload;
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
		}

		async void OnPlayerPostLogin(PlayerPostLoginEventArgs e)
		{
			BankAccount account;

			if ((account = await Bank.FindAccount(e.Player.UserAccountName)) == null && e.Player.Group.HasPermission(Permissions.Permit))
			{
				if (!(await Bank.AddAsync(new BankAccount(e.Player.UserAccountName))))
					Log.ConsoleError("[CurrencyBank] Unable to create bank account for {0}.", e.Player.UserAccountName);
				else
					Log.ConsoleInfo("[CurrencyBank] Bank account created for {0}.", e.Player.UserAccountName);
			}
		}

		async void OnReload(ReloadEventArgs e)
		{
			string cpath = Path.Combine(TShock.SavePath, "CurrencyBank", "Config.json");
			Config = Config.Read(cpath);

			if (await Bank.Reload())
				e.Player.SendSuccessMessage("[CurrencyBank] Database reloaded!");
			else
				e.Player.SendErrorMessage("[CurrencyBank] Database reload failed! Check logs for details.");

		}
	}
}