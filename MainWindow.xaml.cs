using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ServerManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		DirectoryInfo ServerDir = new(@"G:\Auleiy SMP");
		string JavaPath = @"F:\Java\zulu-21\bin\java", ServerFile, BannedIpFile, BannedPlayerFile, EulaFile, OpsFile, PermissionFile, PropertiesFile, WhiteListFile;
		bool CanAddBukkitPlugin, CanAddMod;
		Process ServerMain;
		StreamWriter In;
		StreamReader Out;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void UpdateCommandBox(object sender, TextChangedEventArgs e)
		{
			if (!string.IsNullOrEmpty(CommandBox.Text) && CommandPlaceholder.Visibility != Visibility.Hidden)
				CommandPlaceholder.Visibility = Visibility.Hidden;
			if (string.IsNullOrEmpty(CommandBox.Text) && CommandPlaceholder.Visibility == Visibility.Hidden)
				CommandPlaceholder.Visibility = Visibility.Visible;
		}

		private void SendCommand(object sender, RoutedEventArgs e)
		{
			In.WriteLine(CommandBox.Text);
			AppendText($"> {CommandBox.Text}");
			if (CommandBox.Text.Equals("stop"))
			{
				IsNormallyExit = true;
				StopButton.IsEnabled = false;
				Task.Run(() =>
				{
					ServerMain.WaitForExit();
					ServerMain.Kill();
					ServerMain.Close();
					In.Close();
					Out.Close();
					StartButton.Dispatcher.Invoke(() => StartButton.IsEnabled = true);
					ServerLogBox.Dispatcher.Invoke(AppendText, $"[{DateTime.Now:HH:mm:ss}] [(OutsideServer) ServerManager/INFO]: 服务器已停止");
				});
			}
			CommandBox.Text = "";
		}

		OpenFileDialog ServerPropertiesDialog = new();
		private void BrowseServerProperties(object sender, RoutedEventArgs e)
		{
			ServerPropertiesDialog.DefaultExt = ".properties";
			ServerPropertiesDialog.Filter = "Java properties (.properties)|*.properties";
			ServerPropertiesDialog.InitialDirectory = ServerDir.FullName;
			if (ServerPropertiesDialog.ShowDialog().Value)
			{
				PropertiesFile = ServerPropertiesDialog.FileName;
				ServerPropertiesFilePathBox.Text = PropertiesFile;
			}
		}

		private void WindowLoad(object sender, RoutedEventArgs e)
		{
			ReadServerProfile();
		}

		private void WindowClose(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (ServerMain != null)
			{
				ServerMain.Kill();
				ServerMain.Close();
				In.Close();
				Out.Close();
			}
		}

		#region 服务器控制
		private void Start(object sender, RoutedEventArgs e)
		{
			StartButton.IsEnabled = false;
			StopButton.IsEnabled = true;
			ServerMain = new()
			{
				EnableRaisingEvents = true
			};
			ServerMain.StartInfo.FileName = JavaPath;
			ServerMain.StartInfo.WorkingDirectory = ServerDir.FullName;
			ServerMain.StartInfo.CreateNoWindow = true;
			ServerMain.StartInfo.ArgumentList.Add("-jar");
			ServerMain.StartInfo.ArgumentList.Add(ServerFile);
			ServerMain.StartInfo.ArgumentList.Add("nogui");
			ServerMain.StartInfo.UseShellExecute = false;
			ServerMain.StartInfo.RedirectStandardError = true;
			ServerMain.StartInfo.RedirectStandardInput = true;
			ServerMain.StartInfo.RedirectStandardOutput = true;
			ServerMain.StartInfo.StandardErrorEncoding = Encoding.UTF8;
			ServerMain.StartInfo.StandardInputEncoding = Encoding.UTF8;
			ServerMain.StartInfo.StandardOutputEncoding = Encoding.UTF8;
			ServerMain.Start();
			In = ServerMain.StandardInput;
			Out = ServerMain.StandardOutput;
			ServerMain.Exited += Stop;
			Thread t = new(ConsoleListenThread);
			t.Start();
			CommandBox.IsEnabled = true;
			SendCommandButton.IsEnabled = true;
			ServerLogBox.Document.Blocks.Clear();
		}

		private void AppendText(string message)
		{
			ServerLogBox.AppendText(message + "\n");
			ServerLogBox.CaretPosition = ServerLogBox.CaretPosition.DocumentEnd;
			ServerLogBox.Focus();
		}

		private void ConsoleListenThread()
		{
			try
			{
				while (!Out.EndOfStream)
				{
					string log = Out.ReadLine();
					ServerLogBox.Dispatcher.Invoke(AppendText, log);
				}
			}
			catch { }
		}

		bool IsNormallyExit = false;

		private void Stop(object? sender, EventArgs e)
		{
			if (IsNormallyExit)
				return;
			StopButton.Dispatcher.Invoke(() => StopButton.IsEnabled = false);
			ServerLogBox.Dispatcher.Invoke(AppendText, $"[{DateTime.Now:HH:mm:ss}] [(OutsideServer) ServerManager/WARN]: 服务器非正常退出，可能是崩溃或玩家关闭服务器，请检查日志或玩家行为记录和ops.json。退出代码：0x{ServerMain.ExitCode:X8}");
			ServerMain.Kill();
			ServerMain.Close();
			In.Close();
			Out.Close();
			StartButton.Dispatcher.Invoke(() => StartButton.IsEnabled = true);
		}

		private void Stop(object sender, RoutedEventArgs e)
		{
			if (!ServerMain.HasExited)
			{
				In.WriteLine("stop");
				AppendText("> stop");
			}
			StopButton.IsEnabled = false;
			IsNormallyExit = true;
			Task.Run(() =>
			{
				ServerMain.WaitForExit();
				ServerMain.Kill();
				ServerMain.Close();
				In.Close();
				Out.Close();
				StartButton.Dispatcher.Invoke(() => StartButton.IsEnabled = true);
				ServerLogBox.Dispatcher.Invoke(AppendText, "[{DateTime.Now:HH:mm:ss}] [(OutsideServer) ServerManager/INFO]: 服务器已停止");
			});
		}
		#endregion

		private void ReadServerProfile()
		{
			foreach (FileInfo f in ServerDir.GetFiles())
			{
				if (f.Extension.Equals(".jar"))
					ServerFile = f.FullName;
				switch (f.Name)
				{
					case "banned-ips.json": BannedIpFile = f.FullName; break;
					case "banned-players.json": BannedPlayerFile = f.FullName; break;
					case "eula.txt": EulaFile = f.FullName; break;
					case "ops.json": OpsFile = f.FullName; break;
					case "permissions.yml": PermissionFile = f.FullName; break;
					case "server.properties": PropertiesFile = f.FullName; break;
					case "whitelist.json": WhiteListFile = f.FullName; break;
				}
			}
			if (string.IsNullOrEmpty(ServerFile))
			{
				MessageBox.Show("未找到服务端文件，请手动设置。");
				return;
			}
			ServerNameLabel.Content = $"服务端名称：{ServerFile}";
			ServerPropertiesFilePathBox.Text = PropertiesFile;
			StartButton.IsEnabled = true;
			ReadServerProperties();
		}

		private void UpdateServerProperties(object sender, RoutedEventArgs e)
		{
			allow_flight = AllowFlightControl.IsChecked.Value;
			allow_nether = AllowNetherControl.IsChecked.Value;
			broadcast_console_to_ops = BroadcastConsoleToOpsControl.IsChecked.Value;
			broadcast_rcon_to_ops = BroadcastRconToOpsControl.IsChecked.Value;
			debug = DebugControl.IsChecked.Value;
			difficulty = (Difficulty)DifficultyControl.SelectedIndex;
			enable_command_block = EnableCommandBlockControl.IsChecked.Value;
			enable_jmx_monitoring = EnableJmxMonitoringControl.IsChecked.Value;
			enable_query = EnableQueryControl.IsChecked.Value;
			enable_rcon = EnableRconControl.IsChecked.Value;
			enable_status = EnableStatusControl.IsChecked.Value;
			enforce_secure_profile = EnforceSecureProfileControl.IsChecked.Value;
			enforce_whitelist = EnforceWhitelistControl.IsChecked.Value;
			entity_broadcast_range_percentage = int.Parse(EntityBroadcastRangePercentageControl.Text);
			force_gamemode = ForceGamemodeControl.IsChecked.Value;

			DateTime now = DateTime.Now;
			string[] file = new string[]
			{
				$"#Minecraft server properties",
				$"#{now.DayOfWeek.ToString()[..3]} {now.ToString("MMM dd HH:mm:ss", CultureInfo.CreateSpecificCulture("en-GB"))} CST {now:yyyy}",
				$"allow-flight={allow_flight.ToString().ToLower()}",
				$"allow-nether={allow_nether.ToString().ToLower()}",
				$"broadcast-console-to-ops={broadcast_console_to_ops.ToString().ToLower()}",
				$"broadcast-rcon-to-ops={broadcast_rcon_to_ops.ToString().ToLower()}",
				$"debug={debug.ToString().ToLower()}",
				$"difficulty={difficulty}",
				$"enable-command-block={enable_command_block.ToString().ToLower()}",
				$"enable-jmx-monitoring={enable_jmx_monitoring.ToString().ToLower()}",
				$"enable-query={enable_query.ToString().ToLower()}",
				$"enable-rcon={enable_rcon.ToString().ToLower()}",
				$"enable-status={enable_status.ToString().ToLower()}",
				$"enforce-secure-profile={enforce_secure_profile.ToString().ToLower()}",
				$"enforce-whitelist={enforce_whitelist.ToString().ToLower()}",
				$"entity-broadcast-range-percentage={entity_broadcast_range_percentage.ToString().ToLower()}",
				$"force-gamemode={force_gamemode.ToString().ToLower()}",
			};
			File.WriteAllLines(PropertiesFile, file);
		}

		bool allow_flight, allow_nether, broadcast_console_to_ops, broadcast_rcon_to_ops, debug, enable_command_block, enable_jmx_monitoring, enable_query, enable_rcon, enable_status, enforce_secure_profile, enforce_whitelist, force_gamemode, generate_structures, hardcore, hide_online_players, log_ips, online_mode, prevent_proxy_connections, pvp, require_resource_pack, spawn_animals, spawn_monsters, spawn_npcs, sync_chunk_writes, use_native_transport, white_list;
		Difficulty difficulty;
		int entity_broadcast_range_percentage, function_permission_level, max_chained_neighbor_updates, max_players, max_tick_time, max_world_size, network_compression_threshold, op_permission_level, player_idle_timeout, query_port, rate_limit, rcon_port, server_port, simulation_distance, spawn_protection, view_distance;
		GameMode gamemode;
		string[] initial_disabled_packs, initial_enabled_packs, text_filtering_config;
		string level_name, level_seed, motd, rcon_password, resource_pack, resource_pack_prompt, resource_pack_sha1, server_ip, level_type;
			// level_type must be constant in the WorldType (except custom generation).
		Guid resource_pack_id;
		JsonElement generator_settings;

		private void ReadServerProperties()
		{
			List<string> lines = new(File.ReadAllLines(PropertiesFile));
			foreach (string t in lines)
			{
				if (t.TrimStart().StartsWith('#'))
					continue;

				string[] i = t.Split('=');
				(string key, string value) = (i[0], "");
				for (int j = 0; j < i.Length - 1; ++j)
					value += i[j + 1];

				switch (key)
				{
					case "allow-flight":
						allow_flight = bool.Parse(value);
						break;
					case "allow-nether":
						allow_nether = bool.Parse(value);
						break;
					case "broadcast-console-to-ops":
						broadcast_console_to_ops = bool.Parse(value);
						break;
					case "broadcast-rcon-to-ops":
						broadcast_rcon_to_ops = bool.Parse(value);
						break;
					case "debug":
						debug = bool.Parse(value);
						break;
					case "difficulty":
						difficulty = (Difficulty)Enum.Parse(typeof(Difficulty), value);
						break;
					case "enable-command-block":
						enable_command_block = bool.Parse(value);
						break;
					case "enable-jmx-monitoring":
						enable_jmx_monitoring = bool.Parse(value);
						break;
					case "enable-query":
						enable_query = bool.Parse(value);
						break;
					case "enable-rcon":
						enable_rcon = bool.Parse(value);
						break;
					case "enable-status":
						enable_status = bool.Parse(value);
						break;
					case "enforce-secure-profile":
						enforce_secure_profile = bool.Parse(value);
						break;
					case "enforce-whitelist":
						enforce_whitelist = bool.Parse(value);
						break;
					case "entity-broadcast-range-percentage":
						entity_broadcast_range_percentage = int.Parse(value);
						break;
					case "force-gamemode":
						force_gamemode = bool.Parse(value);
						break;
					case "function-permission-level":
						function_permission_level = int.Parse(value);
						break;
					case "gamemode":
						gamemode = (GameMode)Enum.Parse(typeof(GameMode), value);
						break;
					case "generate-structures":
						generate_structures = bool.Parse(value);
						break;
					case "generator-settings":
						generator_settings = JsonDocument.Parse(value).RootElement;
						break;
					case "hardcore":
						hardcore = bool.Parse(value);
						break;
					case "hide-online-players":
						hide_online_players = bool.Parse(value);
						break;
					case "initial-disabled-packs":
						initial_disabled_packs = value.Split(',');
						break;
					case "initial-enabled-packs":
						initial_enabled_packs = value.Split(',');
						break;
					case "level-name":
						level_name = value;
						break;
					case "level-seed":
						level_seed = value;
						break;
					case "level-type":
						level_type = value;
						break;
					case "log-ips":
						log_ips = bool.Parse(value);
						break;
					case "max-chained-neighbor-updates":
						max_chained_neighbor_updates = int.Parse(value);
						break;
					case "max-players":
						max_players = int.Parse(value);
						break;
					case "max-tick-time":
						max_tick_time = int.Parse(value);
						break;
					case "max-world-size":
						max_world_size = int.Parse(value);
						break;
					case "motd":
						motd = value;
						break;
					case "network-compression-threshold":
						network_compression_threshold = int.Parse(value);
						break;
					case "online-mode":
						online_mode = bool.Parse(value);
						break;
					case "op-permission-level":
						op_permission_level = int.Parse(value);
						break;
					case "player-idle-timeout":
						player_idle_timeout = int.Parse(value);
						break;
					case "prevent-proxy-connections":
						prevent_proxy_connections = bool.Parse(value);
						break;
					case "pvp":
						pvp = bool.Parse(value);
						break;
					case "query.port":
						query_port = int.Parse(value);
						break;
					case "rate-limit":
						rate_limit = int.Parse(value);
						break;
					case "rcon.password":
						rcon_password = value;
						break;
					case "rcon.port":
						rcon_port = int.Parse(value);
						break;
					case "require-resource-pack":
						require_resource_pack = bool.Parse(value);
						break;
					case "resource-pack":
						resource_pack = value;
						break;
					case "resource-pack-id":
						Guid.TryParse(value, out resource_pack_id);
						break;
					case "resource-pack-prompt":
						resource_pack_prompt = value;
						break;
					case "resource-pack-sha1":
						resource_pack_sha1 = value;
						break;
					case "server-ip":
						server_ip = value;
						break;
					case "server-port":
						server_port = int.Parse(value);
						break;
					case "simulation-distance":
						simulation_distance = int.Parse(value);
						break;
					case "spawn-animals":
						spawn_animals = bool.Parse(value);
						break;
					case "spawn-monsters":
						spawn_monsters = bool.Parse(value);
						break;
					case "spawn-npcs":
						spawn_npcs = bool.Parse(value);
						break;
					case "spawn-protection":
						spawn_protection = int.Parse(value);
						break;
					case "sync-chunk-writes":
						sync_chunk_writes = bool.Parse(value);
						break;
					case "text-filtering-config":
						text_filtering_config = value.Split(',');
						break;
					case "use-native-transport":
						use_native_transport = bool.Parse(value);
						break;
					case "view-distance":
						view_distance = int.Parse(value);
						break;
					case "white-list":
						white_list = bool.Parse(value);
						break;
				}
			}

			AllowFlightControl.IsChecked = allow_flight;
			AllowNetherControl.IsChecked = allow_nether;
			BroadcastConsoleToOpsControl.IsChecked = broadcast_console_to_ops;
			BroadcastRconToOpsControl.IsChecked = broadcast_rcon_to_ops;
			DebugControl.IsChecked = debug;
			DifficultyControl.SelectedIndex = (int)difficulty;
			EnableCommandBlockControl.IsChecked = enable_command_block;
			EnableJmxMonitoringControl.IsChecked = enable_jmx_monitoring;
			EnableQueryControl.IsChecked = enable_query;
			EnableRconControl.IsChecked = enable_rcon;
			EnableStatusControl.IsChecked = enable_status;
			EnforceSecureProfileControl.IsChecked = enforce_secure_profile;
			EnforceWhitelistControl.IsChecked = enforce_whitelist;
			EntityBroadcastRangePercentageControl.Text = entity_broadcast_range_percentage.ToString();
			ForceGamemodeControl.IsChecked = force_gamemode;
		}
	}
}
