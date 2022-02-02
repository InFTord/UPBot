﻿using System;
using System.IO;
using System.Data.SQLite;
using System.Reflection;
using System.Collections.Generic;

public class Database {
  static SQLiteConnection connection = null;
  const string DbName = "BotDb";
  static Dictionary<Type, EntityDef> entities;


  public static void InitDb() {
    try {
      // Do we have the db?
      if (File.Exists("Database/" + DbName + ".db"))
        connection = new SQLiteConnection("Data Source=Database/" + DbName + ".db; Version=3; Journal Mode=Off; UTF8Encoding=True;"); // Open the database
      else {
        if (!Directory.Exists("Database")) Directory.CreateDirectory("Database");
        connection = new SQLiteConnection("Data Source=Database/" + DbName + ".db; Version=3; Journal Mode=Off; New=True; UTF8Encoding=True;"); // Create a new database
      }

      // Open the connection
      connection.Open();
      Console.WriteLine("DB connection open");
    } catch (Exception ex) {
      throw new Exception("Cannot open the database: " + ex.Message);
    }

    entities = new Dictionary<Type, EntityDef>();

  }

  public static void AddTable<T>() {
    Type t = typeof(T);
    if (!typeof(Entity).IsAssignableFrom(t))
      throw new Exception("The class " + t + " does not derive from Entity and cannot be used as database table!");

    // Check if we have the table in the db
    string tableName = t.ToString();
    SQLiteCommand command = new SQLiteCommand(connection);
    command.CommandText = "SELECT count(*) FROM " + tableName + ";";
    bool exists = true;
    try {
      SQLiteDataReader reader = command.ExecuteReader();
    } catch (Exception) {
      exists = false;
    }

    if (exists)
      Console.WriteLine("Table " + tableName + " exists.");
    else
      Console.WriteLine("Table " + tableName + " does NOT exist!");

    string theKey = null;
    if (!exists) {
      string sql = "create table " + tableName + " (";
      string index = null;
      foreach (FieldInfo field in t.GetFields()) {
        bool comment = false;
        bool blob = false;
        bool notnull = false;
        bool ignore = false;
        foreach (CustomAttributeData attr in field.CustomAttributes) {
          if (attr.AttributeType == typeof(Entity.Blob)) blob = true;
          if (attr.AttributeType == typeof(Entity.Comment)) comment = true;
          if (attr.AttributeType == typeof(Entity.Key)) {
            notnull = true;
            if (theKey==null) theKey = field.Name;
            else theKey += ", " + field.Name;
          }
          if (attr.AttributeType == typeof(Entity.NotNull)) notnull = true;
          if (attr.AttributeType == typeof(Entity.Index)) {
            if (index == null) index = "CREATE INDEX idx_" + tableName + " ON " + tableName + "(" + field.Name;
            else index += ", " + field.Name;
          }
          if (attr.AttributeType == typeof(Entity.NotPersistent)) ignore = true;
        }
        if (ignore) continue;

        if (blob) sql += field.Name + " BLOB";
        else switch (field.FieldType.Name.ToLowerInvariant()) {
            case "int8": sql += field.Name + " SMALLINT"; break;
            case "uint8": sql += field.Name + " SMALLINT"; break;
            case "byte": sql += field.Name + " SMALLINT"; break;
            case "int32": sql += field.Name + " INT"; break;
            case "int64": sql += field.Name + " BIGINT"; break;
            case "uint64": sql += field.Name + " UNSIGNED BIG INT"; break;
            case "string": {
                if (comment) sql += field.Name + " TEXT";
                else sql += field.Name + " VARCHAR(256)";
                break;
              }
            case "bool": sql += field.Name + " TINYINT"; break;
            case "boolean": sql += field.Name + " TINYINT"; break;
            case "datetime": sql += field.Name + " NUMERIC"; break;
            case "single": sql += field.Name + " REAL"; break;
            case "double": sql += field.Name + " REAL"; break;
            case "byte[]": sql += field.Name + " BLOB"; break;
            default:
              throw new Exception("Unmanaged type: " + field.FieldType.Name + " for class " + t.Name);
          }
        if (notnull) sql += " NOT NULL";
        sql += ", ";
      }
      if (theKey == null) throw new Exception("Missing [Key] for class " + typeof(T));
      sql += " PRIMARY KEY (" + theKey + "));";
      command.CommandText = sql;
      command.ExecuteNonQuery();

      if (index != null) {
        command.CommandText = index;
        command.ExecuteNonQuery();
      }
    }

    // Construct the entity
    EntityDef ed = new EntityDef { type = t };
    List<FieldInfo> keygens = new List<FieldInfo>();
    foreach (FieldInfo field in t.GetFields()) {
      bool blob = false;
      bool ignore = false;
      foreach (CustomAttributeData attr in field.CustomAttributes) {
        if (attr.AttributeType == typeof(Entity.Key)) { keygens.Add(field); }
        if (attr.AttributeType == typeof(Entity.Blob)) { blob = true; }
        if (attr.AttributeType == typeof(Entity.NotPersistent)) { ignore = true; }
      }
      if (ignore) {
        ed.fields[field.Name] = FieldType.IGNORE;
        continue;
      }
      if (blob) ed.fields[field.Name] = FieldType.ByteArray;
      else switch (field.FieldType.Name.ToLowerInvariant()) {
          case "int8": ed.fields[field.Name] = FieldType.Byte; break;
          case "uint8": ed.fields[field.Name] = FieldType.Byte; break;
          case "byte": ed.fields[field.Name] = FieldType.Byte; break;
          case "int32": ed.fields[field.Name] = FieldType.Int; break;
          case "int64": ed.fields[field.Name] = FieldType.Long; break;
          case "uint64": ed.fields[field.Name] = FieldType.ULong; break;
          case "string": {
              if (blob) ed.fields[field.Name] = FieldType.Blob;
              else ed.fields[field.Name] = FieldType.String;
              break;
            }
          case "bool": ed.fields[field.Name] = FieldType.Bool; break;
          case "boolean": ed.fields[field.Name] = FieldType.Bool; break;
          case "datetime": ed.fields[field.Name] = FieldType.Date; break;
          case "single": ed.fields[field.Name] = FieldType.Float; break;
          case "double": ed.fields[field.Name] = FieldType.Double; break;
          case "byte[]": ed.fields[field.Name] = FieldType.ByteArray; break;
          default:
            throw new Exception("Unmanaged type: " + field.FieldType.Name + " for class " + t.Name);
        }
    }
    if (keygens.Count == 0) throw new Exception("Missing key for class " + t);
    ed.keys = keygens.ToArray();

    // Build the query strings
    theKey = "";
    int keynum = 1;
    foreach (var key in keygens) {
      if (theKey.Length > 0) theKey += " and ";
      theKey += key.Name + "=@param" + keynum;
      keynum++;
    }

    ed.count = "SELECT Count(*) FROM " + t.ToString() + " WHERE " + theKey;
    ed.select = "SELECT * FROM " + t.ToString();
    ed.delete = "DELETE FROM " + t.Name + " WHERE " + theKey;

    // Insert, Update
    string insert = "INSERT INTO " + t.ToString() + " (";
    string insertpost = ") VALUES (";
    string update = "UPDATE " + t.ToString() + " SET ";
    bool donefirst = false;
    foreach (FieldInfo field in t.GetFields()) {
      bool ignore = false;
      foreach (CustomAttributeData attr in field.CustomAttributes) {
        if (attr.AttributeType == typeof(Entity.NotPersistent)) { ignore = true;  break; }
      }
      if (ignore) continue;

      if (donefirst) { insert += ", "; insertpost += ", "; update += ", "; } else donefirst = true;
      insert += field.Name;
      insertpost += "@p" + field.Name;
      update += field.Name + "=@p" + field.Name;
    }
    ed.insert = insert + insertpost + ");";
    ed.update = update + " WHERE " + theKey;
    entities.Add(t, ed);
  }

  public static int Count<T>() {
    SQLiteCommand cmd = new SQLiteCommand("SELECT count(*) FROM " + typeof(T), connection);
    return Convert.ToInt32(cmd.ExecuteScalar());
  }

  public static void Update<T>(T val) {
    Add(val);
  }
  public static void Insert<T>(T val) {
    Add(val);
  }

  public static void Add<T>(T val) {
    try {
      Type t = val.GetType();
      EntityDef ed = entities[t];
      // Get the values with this key from the db
      SQLiteCommand cmd = new SQLiteCommand(ed.count, connection);
      AddKeyParams(ed, cmd, val);

      // Do we have our value?
      if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) { // Yes -> Update
        SQLiteCommand update = new SQLiteCommand(ed.update, connection);
        foreach (FieldInfo field in t.GetFields()) {
          update.Parameters.Add(new SQLiteParameter("@p" + field.Name, field.GetValue(val)));
        }
        AddKeyParams(ed, update, val);
        update.ExecuteNonQuery();
      } else { // No - Insert
        SQLiteCommand insert = new SQLiteCommand(ed.insert, connection);
        foreach (FieldInfo field in t.GetFields()) {
          insert.Parameters.Add(new SQLiteParameter("@p" + field.Name, field.GetValue(val)));
        }
        insert.ExecuteNonQuery();
      }
    } catch (Exception ex) {
      Utils.Log("Error in Adding data for " + val.GetType() + ": " + ex.Message);
    }
  }

  private static void AddKeyParams(EntityDef ed, SQLiteCommand cmd, object val) {
    int num = 1;
    foreach (var key in ed.keys) {
      object kv = key.GetValue(val);
      cmd.Parameters.Add(new SQLiteParameter("@param" + num, kv));
      num++;
    }
  }

  public static void Delete<T>(T val) {
    try {
      EntityDef ed = entities[val.GetType()];
      SQLiteCommand cmd = new SQLiteCommand(ed.delete, connection);
      AddKeyParams(ed, cmd, val);
      cmd.ExecuteNonQuery();
    } catch (Exception ex) {
      Utils.Log("Error in Deleting data for " + val.GetType() + ": " + ex.Message);
    }
  }
  public static void DeleteByKeys<T>(params object[] keys) {
    try {
      EntityDef ed = entities[typeof(T)];
      SQLiteCommand cmd = new SQLiteCommand(ed.delete, connection);
      if (ed.keys.Length != keys.Length) throw new Exception("Inconsistent number of keys for: " + typeof(T).FullName);
      int num = 0;
      foreach (var key in ed.keys) {
        cmd.Parameters.Add(new SQLiteParameter("@param" + (num+1), keys[num]));
        num++;
      }
      cmd.ExecuteNonQuery();
    } catch (Exception ex) {
      Utils.Log("Error in Deleting data for " + typeof(T) + ": " + ex.Message);
    }
  }


  public static List<T> GetAll<T>() {
    try {
      Type t = typeof(T);
      EntityDef ed = entities[t];
      SQLiteCommand cmd = new SQLiteCommand(ed.select + ";", connection);
      SQLiteDataReader reader = cmd.ExecuteReader();
      List<T> res = new List<T>();
      while (reader.Read()) {
        T val = (T)Activator.CreateInstance(t);
        int num = 0;
        foreach (FieldInfo field in t.GetFields()) {
          FieldType ft = ed.fields[field.Name];
          if (!reader.IsDBNull(num)) {
            switch (ft) {
              case FieldType.Bool: field.SetValue(val, reader.GetByte(num) != 0); break;
              case FieldType.Byte: field.SetValue(val, reader.GetByte(num)); break;
              case FieldType.Int: field.SetValue(val, reader.GetInt32(num)); break;
              case FieldType.Long: field.SetValue(val, reader.GetInt64(num)); break;
              case FieldType.ULong: field.SetValue(val, (ulong)reader.GetInt64(num)); break;
              case FieldType.String: field.SetValue(val, reader.GetString(num)); break;
              case FieldType.Comment: field.SetValue(val, reader.GetString(num)); break;
              case FieldType.Date: field.SetValue(val, reader.GetDateTime(num)); break;
              case FieldType.Float: field.SetValue(val, reader.GetFloat(num)); break;
              case FieldType.Double: field.SetValue(val, reader.GetDouble(num)); break;
              case FieldType.Blob:
              case FieldType.ByteArray:
                field.SetValue(val, (byte[])reader[field.Name]);
                break;
            }
          }
          num++;
        }
        res.Add(val);
      }
      return res;
    } catch (Exception ex) {
      Utils.Log(" " + typeof(T) + ": " + ex.Message);
    }
    return null;
  }


  /*
  GetValue
  GetAllValues


   */

  class EntityDef {
    public Type type;
    public FieldInfo[] keys;
    public Dictionary<string, FieldType> fields = new Dictionary<string, FieldType>();
    public string count;
    public string select;
    public string insert;
    public string update;
    public string delete;
  }

  enum FieldType {
    IGNORE,
    Bool,
    Byte,
    Int,
    Long,
    ULong,
    String,
    Comment,
    Date,
    Float,
    Double,
    Blob,
    ByteArray,
  }
}

