﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Utility functions that don't belong to a specific class or a specific command
/// "General-purpose" function, which can be needed anywhere.
/// </summary>
public static class Utils
{
  public const int vmajor = 0, vminor = 2, vbuild = 1;
  public const char vrev = 'c';
  public static string LogsFolder = "./";

  /// <summary>
  /// Common colors
  /// </summary>
  public static readonly DiscordColor Red = new DiscordColor("#f50f48");
  public static readonly DiscordColor Green = new DiscordColor("#32a852");
  public static readonly DiscordColor LightBlue = new DiscordColor("#34cceb");
  public static readonly DiscordColor Yellow = new DiscordColor("#f5bc42");
  
  // Fields relevant for InitClient()
  private static DiscordClient client;
  private static DateTimeFormatInfo sortableDateTimeFormat;

  private class LogInfo {
    public StreamWriter sw;
    public string path;
  }

  readonly private static Dictionary<string, LogInfo> logs = new Dictionary<string, LogInfo>();

  public static string GetVersion() {
    return vmajor + "." + vminor + "." + vbuild + vrev + " - 2022/04/28";
  }

  public static DiscordClient GetClient() {
    return client;
  }

  public static void InitClient(DiscordClient c) {
    client = c;
    if (!DiscordEmoji.TryFromName(client, ":thinking:", out thinkingAsError)) {
      thinkingAsError = DiscordEmoji.FromUnicode("🤔");
    }
    emojiNames = new[] {
      ":thinking:", // Thinking = 0,
      ":OK:", // OK = 1,
      ":KO:", // KO = 2,
      ":whatthisguysaid:", // whatthisguysaid = 3,
      ":StrongSmile:", // StrongSmile = 4,
      ":CPP:", // Cpp = 5,
      ":CSharp:", // CSharp = 6,
      ":Java:", // Java = 7,
      ":Javascript:", // Javascript = 8,
      ":Python:", // Python = 9,
      ":UnitedProgramming:", // UnitedProgramming = 10,
      ":Unity:", // Unity = 11,
      ":Godot:", // Godot = 12,
      ":AutoRefactored:",  // AutoRefactored = 13,
      ":CodeMonkey:", // CodeMonkey = 14,
      ":TaroDev:", // TaroDev = 15,
    };
    emojiUrls = new string[emojiNames.Length];
    emojiSnowflakes = new string[emojiNames.Length];
    sortableDateTimeFormat = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;
  }

  public static void InitLogs(string guild) {
    string logPath = Path.Combine(LogsFolder, "BotLogs " + guild + " " + DateTime.Now.ToString("yyyyMMdd") + ".logs");
    LogInfo l;
    if (logs.ContainsKey(guild)) l = logs[guild];
    else {
      l = new LogInfo();
      logs[guild] = l;
    }
    l.path = logPath;
    if (File.Exists(logPath)) logs[guild].sw = new StreamWriter(logPath, append: true);
    else logs[guild].sw = File.CreateText(logPath);
  }

  public static string GetLogsPath(string guild) {
    if (!logs.ContainsKey(guild)) return null;
    return logs[guild].path;
  }

  public static string GetLastLogsFolder(string guild, string logPath) {
    string zipFolder = Path.Combine(LogsFolder, guild + " ZippedLog/");
    if (!Directory.Exists(zipFolder)) Directory.CreateDirectory(zipFolder);
    FileInfo fi = new FileInfo(logPath);
    File.Copy(fi.FullName, Path.Combine(zipFolder, fi.Name), true);
    return zipFolder;
  }

  public static string GetAllLogsFolder(string guild) {
    Regex logsRE = new Regex(@"BotLogs\s" + guild + @"\s[0-9]{8}\.logs", RegexOptions.IgnoreCase);
    string zipFolder = Path.Combine(LogsFolder, guild + " ZippedLogs/");
    if (!Directory.Exists(zipFolder)) Directory.CreateDirectory(zipFolder);
    foreach (var file in Directory.GetFiles(LogsFolder, "*.logs")) {
      if (logsRE.IsMatch(file)) {
        FileInfo fi = new FileInfo(file);
        File.Copy(fi.FullName, Path.Combine(zipFolder, fi.Name), true);
      }
    }
    return zipFolder;
  }

  public static int DeleteAllLogs(string guild) {
    Regex logsRE = new Regex(@"BotLogs\s" + guild + @"\s[0-9]{8}\.logs", RegexOptions.IgnoreCase);
    List<string> toDelete = new List<string>();
    foreach (var file in Directory.GetFiles(LogsFolder, "*.logs")) {
      if (logsRE.IsMatch(file)) {
        FileInfo fi = new FileInfo(file);
        toDelete.Add(fi.FullName);
      }
    }
    int num = 0;
    foreach (var file in toDelete) {
      try {
        File.Delete(file);
        num++;
      } catch { }
    }
    return num;
  }

  internal static string GetSafeMemberName(CommandContext ctx, ulong userSnoflake) {
    try {
      return ctx.Guild.GetMemberAsync(userSnoflake).Result.DisplayName;
    } catch (Exception e) {
      Log("Invalid user snowflake: " + userSnoflake + " -> " + e.Message, ctx.Guild.Name);
      return null;
    }
  }

  /// <summary>
  /// Change a string based on the count it's referring to (e.g. "one apple", "two apples")
  /// </summary>
  /// <param name="count">The count, the string is referring to</param>
  /// <param name="singular">The singular version (referring to only one)</param>
  /// <param name="plural">The singular version (referring to more than one)</param>
  public static string PluralFormatter(int count, string singular, string plural) {
    return count > 1 ? plural : singular;
  }

  /// <summary>
  /// Builds a Discord embed with a given TITLE, DESCRIPTION and COLOR
  /// </summary>
  /// <param name="title">Embed title</param>
  /// <param name="description">Embed description</param>
  /// <param name="color">Embed color</param>
  public static DiscordEmbedBuilder BuildEmbed(string title, string description, DiscordColor color) {
    return new DiscordEmbedBuilder {
      Title = title,
      Color = color,
      Description = description
    };
  }

  /// <summary>
  /// Builds a Discord embed with a given TITLE, DESCRIPTION and COLOR
  /// and SENDS the embed as a message
  /// </summary>
  /// <param name="title">Embed title</param>
  /// <param name="description">Embed description</param>
  /// <param name="color">Embed color</param>
  /// <param name="ctx">CommandContext, required to send a message</param>
  /// <param name="respond">Respond to original message or send an independent message?</param>
  public static async Task<DiscordMessage> BuildEmbedAndExecute(string title, string description, DiscordColor color, 
    CommandContext ctx, bool respond)
  {
    var embedBuilder = BuildEmbed(title, description, color);
    return await LogEmbed(embedBuilder, ctx, respond);
  }

  /// <summary>
  /// Quick shortcut to generate an error message
  /// </summary>
  /// <param name="error">The error to display</param>
  /// <returns></returns>
  internal static DiscordEmbed GenerateErrorAnswer(string guild, string cmd, Exception exception) {
    DiscordEmbedBuilder e = new DiscordEmbedBuilder {
      Color = Red,
      Title = "Error in " + cmd,
      Description = exception.Message
    };
    Log("Error in " + cmd + ": " + exception.Message, guild);
    return e.Build();
  }

  /// <summary>
  /// Logs an embed as a message in the relevant channel
  /// </summary>
  /// <param name="builder">Embed builder with the embed template</param>
  /// <param name="ctx">CommandContext, required to send a message</param>
  /// <param name="respond">Respond to original message or send an independent message?</param>
  public static async Task<DiscordMessage> LogEmbed(DiscordEmbedBuilder builder, CommandContext ctx, bool respond)
  {
    if (respond)
      return await ctx.RespondAsync(builder.Build());

    return await ctx.Channel.SendMessageAsync(builder.Build());
  } 

  private static string[] emojiNames;
  private static string[] emojiUrls;
  private static string[] emojiSnowflakes;
  private static DiscordEmoji thinkingAsError;

  /// <summary>
  /// This function gets the Emoji object corresponding to the emojis of the server.
  /// They are cached to improve performance (this command will not work on other servers.)
  /// </summary>
  /// <param name="emoji">The emoji to get, specified from the enum</param>
  /// <returns>The requested emoji or the Thinking emoji in case something went wrong</returns>
  public static DiscordEmoji GetEmoji(EmojiEnum emoji) {
    int index = (int)emoji;
    if (index < 0 || index >= emojiNames.Length) {
      Console.WriteLine("WARNING: Requested wrong emoji");
      return thinkingAsError;
    }
    if (!DiscordEmoji.TryFromName(client, emojiNames[index], out DiscordEmoji res)) {
      Console.WriteLine("WARNING: Cannot get requested emoji: " + emoji.ToString());
      return thinkingAsError;
    }
    return res;
  }


  /// <summary>
  /// This function gets the url of the Emoji based on its name.
  /// No access to discord (so if the URL is no more valid it will fail (invalid image))
  /// </summary>
  /// <param name="emoji">The emoji to get, specified from the enum</param>
  /// <returns>The requested url for the emoji</returns>
  public static string GetEmojiURL(EmojiEnum emoji) {
    int index = (int)emoji;
    if (index < 0 || index >= emojiNames.Length) {
      Console.WriteLine("WARNING: Requested wrong emoji");
      return thinkingAsError.Url;
    }

    if (!string.IsNullOrEmpty(emojiUrls[index])) return emojiUrls[index];
    if (!DiscordEmoji.TryFromName(client, emojiNames[index], out DiscordEmoji res)) {
      Console.WriteLine("WARNING: Cannot get requested emoji: " + emoji.ToString());
      return thinkingAsError;
    }
    emojiUrls[index] = res.Url;
    return res.Url;
  }

  /// <summary>
  /// Used to get the <:UnitedProgramming:831407996453126236> format of an emoji object
  /// </summary>
  /// <param name="emoji">The emoji to convert</param>
  /// <returns>A string representation of the emoji that can be used in a message</returns>
  public static string GetEmojiSnowflakeID(EmojiEnum emoji) {
    int index = (int)emoji;
    if (index < 0 || index >= emojiNames.Length) {
      return "<" + thinkingAsError.GetDiscordName() + thinkingAsError.Id.ToString() + ">";
    }

    if (!string.IsNullOrEmpty(emojiSnowflakes[index])) return emojiSnowflakes[index];
    if (!DiscordEmoji.TryFromName(client, emojiNames[index], out DiscordEmoji res)) {
      Console.WriteLine("WARNING: Cannot get requested emoji: " + emoji.ToString());
      return thinkingAsError;
    }
    emojiSnowflakes[index] = "<" + res.GetDiscordName() + res.Id.ToString() + ">";
    return emojiSnowflakes[index];
  }

  /// <summary>
  /// Used to get the <:UnitedProgramming:831407996453126236> format of an emoji object
  /// </summary>
  /// <param name="emoji">The emoji to convert</param>
  /// <returns>A string representation of the emoji that can be used in a message</returns>
  public static string GetEmojiSnowflakeID(DiscordEmoji emoji) {
    if (emoji == null) return "";
    return "<" + emoji.GetDiscordName() + emoji.Id.ToString() + ">";
  }


  /// <summary>
  /// Used to get the <:UnitedProgramming:831407996453126236> format of an emoji identified by id or name
  /// </summary>
  /// <param name="id">The emoji id, if zero then the name is used</param>
  /// <param name="name">The emoji in Unicode format</param>
  /// <returns>A string representation of the emoji that can be used in a message</returns>
  public static string GetEmojiSnowflakeID(ulong id, string name, DiscordGuild g) {
    if (id == 0) return name;
    var em = g.GetEmojiAsync(id).Result;
    if (em == null) return "?";
    return "<" + em.GetDiscordName() + em.Id.ToString() + ">";
  }

  /// <summary>
  /// Adds a line in the logs telling which user used what command
  /// </summary>
  /// <param name="ctx"></param>
  /// <returns></returns>
  internal static void LogUserCommand(CommandContext ctx) {
    Log(DateTime.Now.ToString(sortableDateTimeFormat.SortableDateTimePattern) + 
      "=> " + ctx.Command.Name + 
      " FROM " + ctx.Member.DisplayName + 
      ": " + ctx.Message.Content,
      ctx.Guild.Name);
  }

  internal static void LogUserCommand(InteractionContext ctx) {
    string log = $"{DateTime.Now.ToString(sortableDateTimeFormat.SortableDateTimePattern)} => {ctx.CommandName} FROM {ctx.Member.DisplayName}";
    if (ctx.Interaction.Data.Options != null)
      foreach (var p in ctx.Interaction.Data.Options) log += $" [{p.Name}]{p.Value}";
    Log(log, ctx.Guild.Name);
  }

  /// <summary>
  /// Logs a text in the console
  /// </summary>
  /// <param name="msg"></param>
  /// <returns></returns>
    internal static void Log(string msg, string guild) {
    if (guild == null) guild = "GLOBAL";
    Console.WriteLine(guild + ": " + msg);
    try {
      if (!logs.ContainsKey(guild)) InitLogs(guild);
      logs[guild].sw.WriteLine(msg);
      logs[guild].sw.FlushAsync();
    } catch (Exception e) {
      Console.WriteLine("Log error: " + e.Message);
    }
  }

  internal static async Task ErrorCallback(CommandErrors error, CommandContext ctx, params object[] additionalParams) {
    DiscordColor red = Red;
    string message = string.Empty;
    bool respond = false;
    switch (error) {
      case CommandErrors.CommandExists:
        respond = true;
        if (additionalParams[0] is string name)
          message = $"There is already a command containing the alias {name}";
        else
          throw new System.ArgumentException("This error type 'CommandErrors.CommandExists' requires a string");
        break;
      case CommandErrors.UnknownError:
        message = "Unknown error!";
        respond = false;
        break;
      case CommandErrors.InvalidParams:
        message = "The given parameters are invalid. Enter `\\help [commandName]` to get help with the usage of the command.";
        respond = true;
        break;
      case CommandErrors.InvalidParamsDelete:
        if (additionalParams[0] is int count)
          message = $"You can't delete {count} messages. Try to eat {count} apples, does that make sense?";
        else
          goto case CommandErrors.InvalidParams;
        break;
      case CommandErrors.MissingCommand:
        message = "There is no command with this name! If it's a CC, please don't use an alias, use the original name!";
        respond = true;
        break;
      case CommandErrors.NoCustomCommands:
        message = "There are no CC's currently.";
        respond = false;
        break;
      case CommandErrors.CommandNotSpecified:
        message = "No command name was specified. Enter `\\help ccnew` to get help with the usage of the command.";
        respond = true;
        break;
    }

    await Utils.BuildEmbedAndExecute("Error", message, red, ctx, respond);
  }

  /// <summary>
  /// Used to delete a folder after a while
  /// </summary>
  /// <param name="msg1"></param>
  public static Task DeleteFolderDelayed(int seconds, string path) {
    Task.Run(() => {
      try {
        Task.Delay(seconds * 1000).Wait();
        Directory.Delete(path, true);
      } catch (Exception ex) {
        Console.WriteLine("Cannot delete folder: " + path + ": " + ex.Message);
      }
    });
    return Task.FromResult(0);
  }

  /// <summary>
  /// Used to delete a file after a while
  /// </summary>
  /// <param name="msg1"></param>
  public static Task DeleteFileDelayed(int seconds, string path) {
    Task.Run(() => {
      try {
        Task.Delay(seconds * 1000).Wait();
        File.Delete(path);
      } catch (Exception ex) {
        Console.WriteLine("Cannot delete file: " + path + ": " + ex.Message);
      }
    });
    return Task.FromResult(0);
  }

  /// <summary>
  /// Used to delete some messages after a while
  /// </summary>
  /// <param name="msg1"></param>
  public static Task DeleteDelayed(int seconds, DiscordMessage msg1) {
    Task.Run(() => DelayAfterAWhile(msg1, seconds * 1000));
    return Task.FromResult(0);
  }
  public static Task DeleteDelayedSend(int seconds, DiscordChannel ch, string msg) {
    if (msg.Length > 1999) { // Split
      List<DiscordMessage> msgs = new List<DiscordMessage>();
      while (msg.Length > 1999) {
        int pos = msg.LastIndexOf(' ', 2000);
        if (pos == -1) pos = 1990;
        string msg1 = msg[0..pos].Trim();
        msg = msg[pos..].Trim();
        msgs.Add(ch.SendMessageAsync(msg1).Result);
      }
      if (msg.Length > 0) msgs.Add(ch.SendMessageAsync(msg).Result);
      foreach (var dm in msgs) {
        Task.Run(() => DelayAfterAWhile(dm, seconds * 1000));
      }
    }
    else {
      DiscordMessage dmsg = ch.SendMessageAsync(msg).Result;
      Task.Run(() => DelayAfterAWhile(dmsg, seconds * 1000));
    }
    return Task.FromResult(0);
  }

  /// <summary>
  /// Used to delete some messages after a while
  /// </summary>
  /// <param name="tmsg"></param>
  public static Task DeleteDelayed(int seconds, Task<DiscordMessage> tmsg) {
    Task.Run(() => DelayAfterAWhile(tmsg.Result, seconds * 1000));
    return Task.FromResult(0);
  }

  /// <summary>
  /// Used to delete some messages after a while
  /// </summary>
  /// <param name="msg1"></param>
  /// <param name="msg2"></param>
  public static Task DeleteDelayed(int seconds, DiscordMessage msg1, DiscordMessage msg2) {
    Task.Run(() => DelayAfterAWhile(msg1, seconds * 1000));
    Task.Run(() => DelayAfterAWhile(msg2, seconds * 1000));
    return Task.FromResult(0);
  }

  /// <summary>
  /// Used to delete some messages after a while
  /// </summary>
  /// <param name="msg1"></param>
  /// <param name="msg2"></param>
  /// <param name="msg3"></param>
  public static Task DeleteDelayed(int seconds, DiscordMessage msg1, DiscordMessage msg2, DiscordMessage msg3) {
    Task.Run(() => DelayAfterAWhile(msg1, seconds * 1000));
    Task.Run(() => DelayAfterAWhile(msg2, seconds * 1000));
    Task.Run(() => DelayAfterAWhile(msg3, seconds * 1000));
    return Task.FromResult(0);
  }

  static void DelayAfterAWhile(DiscordMessage msg, int delay) {
    try {
      Task.Delay(delay).Wait();
      msg.DeleteAsync().Wait();
    } catch (Exception) { }
  }

}

public enum EmojiEnum {
  None = -1,
  Thinking = 0,
  OK = 1,
  KO = 2,
  WhatThisGuySaid = 3,
  StrongSmile = 4,
  Cpp = 5,
  CSharp = 6,
  Java = 7,
  Javascript = 8,
  Python = 9,
  UnitedProgramming = 10,
  Unity = 11,
  Godot = 12,
  AutoRefactored = 13,
  CodeMonkey = 14,
  TaroDev = 15,
}

public enum CommandErrors {
  InvalidParams,
  InvalidParamsDelete,
  CommandExists,
  UnknownError,
  MissingCommand,
  NoCustomCommands,
  CommandNotSpecified
}
