using System;
using System.Collections.Generic;
using System.Data;
using WpfApp1.Models;
using Microsoft.Data.Sqlite;

namespace WpfApp1.Database;

public class HardwareDatabase : IDisposable
{
    private readonly string _connectionString;

    public HardwareDatabase(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS hardware (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                inventory_number TEXT NOT NULL UNIQUE,
                type TEXT NOT NULL,
                manufacturer TEXT NOT NULL DEFAULT '',
                model TEXT NOT NULL DEFAULT '',
                serial_number TEXT NOT NULL DEFAULT '',
                commission_date TEXT NOT NULL DEFAULT '',
                cost REAL NOT NULL DEFAULT 0,
                location TEXT NOT NULL DEFAULT '',
                responsible_user TEXT NOT NULL DEFAULT '',
                condition TEXT NOT NULL DEFAULT 'Рабочее'
            );

            CREATE TABLE IF NOT EXISTS work_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hardware_id INTEGER NOT NULL,
                event_date TEXT NOT NULL DEFAULT '',
                event_type TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                cost REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (hardware_id) REFERENCES hardware(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS _migrations (
                name TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM _migrations WHERE name = 'rub_to_byn'";
        if (Convert.ToInt32(checkCmd.ExecuteScalar()) == 0)
        {
            using var convCmd = conn.CreateCommand();
            convCmd.CommandText = "UPDATE hardware SET cost = ROUND(cost / 29.0, 2) WHERE cost > 0";
            convCmd.ExecuteNonQuery();

            using var insCmd = conn.CreateCommand();
            insCmd.CommandText = "INSERT INTO _migrations (name) VALUES ('rub_to_byn')";
            insCmd.ExecuteNonQuery();
        }

        using var checkColCmd = conn.CreateCommand();
        checkColCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('work_log') WHERE name='status'";
        if (Convert.ToInt32(checkColCmd.ExecuteScalar()) == 0)
        {
            using var alter1 = conn.CreateCommand();
            alter1.CommandText = "ALTER TABLE work_log ADD COLUMN status TEXT NOT NULL DEFAULT 'В работе'";
            alter1.ExecuteNonQuery();

            using var alter2 = conn.CreateCommand();
            alter2.CommandText = "ALTER TABLE work_log ADD COLUMN notes TEXT NOT NULL DEFAULT ''";
            alter2.ExecuteNonQuery();

            using var alter3 = conn.CreateCommand();
            alter3.CommandText = "ALTER TABLE work_log ADD COLUMN additional_cost REAL NOT NULL DEFAULT 0";
            alter3.ExecuteNonQuery();

            using var insCmd = conn.CreateCommand();
            insCmd.CommandText = "INSERT INTO _migrations (name) VALUES ('work_log_ext')";
            insCmd.ExecuteNonQuery();
        }

        using var renameCmd = conn.CreateCommand();
        renameCmd.CommandText = "SELECT COUNT(*) FROM _migrations WHERE name = 'rename_values'";
        if (Convert.ToInt32(renameCmd.ExecuteScalar()) == 0)
        {
            using var up1 = conn.CreateCommand();
            up1.CommandText = "UPDATE hardware SET condition = 'Неисправен' WHERE condition = 'Требует ремонта'";
            up1.ExecuteNonQuery();

            using var up2 = conn.CreateCommand();
            up2.CommandText = "UPDATE hardware SET type = 'Сетевое' WHERE type = 'Сетевое оборудование'";
            up2.ExecuteNonQuery();

            using var insCmd = conn.CreateCommand();
            insCmd.CommandText = "INSERT INTO _migrations (name) VALUES ('rename_values')";
            insCmd.ExecuteNonQuery();
        }
    }

    private SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON";
        cmd.ExecuteNonQuery();
        return conn;
    }

    private static HardwareItem ReadHardware(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        InventoryNumber = r.GetString(1),
        Type = r.GetString(2),
        Manufacturer = r.GetString(3),
        Model = r.GetString(4),
        SerialNumber = r.GetString(5),
        CommissionDate = r.IsDBNull(6) || r.GetString(6) == "" ? DateTime.Today : DateTime.Parse(r.GetString(6)),
        Cost = Convert.ToDecimal(r.GetDouble(7)),
        Location = r.GetString(8),
        ResponsibleUser = r.GetString(9),
        Condition = r.GetString(10)
    };

    public List<HardwareItem> GetAll()
    {
        var items = new List<HardwareItem>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM hardware ORDER BY id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(ReadHardware(r));
        return items;
    }

    public List<string> GetLocations()
    {
        var locations = new List<string>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT location FROM hardware WHERE location != '' ORDER BY location";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            locations.Add(r.GetString(0));
        return locations;
    }

    public List<HardwareItem> Search(string? query, string? typeFilter, string? conditionFilter, string? locationFilter = null)
    {
        var sql = "SELECT * FROM hardware WHERE 1=1";
        var args = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += " AND (inventory_number LIKE @q OR model LIKE @q OR responsible_user LIKE @q OR serial_number LIKE @q OR location LIKE @q OR type LIKE @q OR manufacturer LIKE @q)";
            args.Add(new SqliteParameter("@q", $"%{query}%"));
        }
        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "Все")
        {
            if (typeFilter == "Другое")
            {
                sql += " AND type NOT IN ('Компьютер','Ноутбук','Монитор','Принтер','МФУ','Сетевое оборудование','Сетевое','Сервер','ИБП')";
            }
            else
            {
                sql += " AND type = @t";
                args.Add(new SqliteParameter("@t", typeFilter));
            }
        }
        if (!string.IsNullOrWhiteSpace(conditionFilter) && conditionFilter != "Все")
        {
            sql += " AND condition = @c";
            args.Add(new SqliteParameter("@c", conditionFilter));
        }
        if (!string.IsNullOrWhiteSpace(locationFilter) && locationFilter != "Все")
        {
            sql += " AND location = @l";
            args.Add(new SqliteParameter("@l", locationFilter));
        }

        sql += " ORDER BY id DESC";

        var items = new List<HardwareItem>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(args);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(ReadHardware(r));
        return items;
    }

    public HardwareItem? GetById(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM hardware WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadHardware(r) : null;
    }

    public int Add(HardwareItem item)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO hardware (inventory_number, type, manufacturer, model, serial_number,
                                  commission_date, cost, location, responsible_user, condition)
            VALUES (@inv, @type, @manuf, @model, @serial, @date, @cost, @loc, @user, @cond)
            """;
        cmd.Parameters.AddWithValue("@inv", item.InventoryNumber);
        cmd.Parameters.AddWithValue("@type", item.Type);
        cmd.Parameters.AddWithValue("@manuf", item.Manufacturer);
        cmd.Parameters.AddWithValue("@model", item.Model);
        cmd.Parameters.AddWithValue("@serial", item.SerialNumber);
        cmd.Parameters.AddWithValue("@date", item.CommissionDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@cost", (double)item.Cost);
        cmd.Parameters.AddWithValue("@loc", item.Location);
        cmd.Parameters.AddWithValue("@user", item.ResponsibleUser);
        cmd.Parameters.AddWithValue("@cond", item.Condition);
        cmd.ExecuteNonQuery();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(cmd2.ExecuteScalar());
    }

    public void Update(HardwareItem item)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE hardware SET inventory_number=@inv, type=@type, manufacturer=@manuf, model=@model,
                                serial_number=@serial, commission_date=@date, cost=@cost,
                                location=@loc, responsible_user=@user, condition=@cond
            WHERE id=@id
            """;
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@inv", item.InventoryNumber);
        cmd.Parameters.AddWithValue("@type", item.Type);
        cmd.Parameters.AddWithValue("@manuf", item.Manufacturer);
        cmd.Parameters.AddWithValue("@model", item.Model);
        cmd.Parameters.AddWithValue("@serial", item.SerialNumber);
        cmd.Parameters.AddWithValue("@date", item.CommissionDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@cost", (double)item.Cost);
        cmd.Parameters.AddWithValue("@loc", item.Location);
        cmd.Parameters.AddWithValue("@user", item.ResponsibleUser);
        cmd.Parameters.AddWithValue("@cond", item.Condition);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM work_log WHERE hardware_id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM hardware WHERE id = @id";
        cmd.ExecuteNonQuery();
    }

    public List<WorkLogEntry> GetWorkLog(int hardwareId)
    {
        var entries = new List<WorkLogEntry>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, hardware_id, event_date, event_type, description, cost,
                   COALESCE(status, 'В работе'), COALESCE(notes, ''), COALESCE(additional_cost, 0)
            FROM work_log WHERE hardware_id = @hid ORDER BY event_date DESC, id DESC
            """;
        cmd.Parameters.AddWithValue("@hid", hardwareId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            entries.Add(new WorkLogEntry
            {
                Id = r.GetInt32(0),
                HardwareId = r.GetInt32(1),
                EventDate = r.IsDBNull(2) || r.GetString(2) == "" ? DateTime.Today : DateTime.Parse(r.GetString(2)),
                EventType = r.GetString(3),
                Description = r.GetString(4),
                Cost = Convert.ToDecimal(r.GetDouble(5)),
                Status = r.GetString(6),
                Notes = r.GetString(7),
                AdditionalCost = Convert.ToDecimal(r.GetDouble(8))
            });
        }
        return entries;
    }

    public void UpdateCondition(int hardwareId, string condition)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE hardware SET condition = @cond WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", hardwareId);
        cmd.Parameters.AddWithValue("@cond", condition);
        cmd.ExecuteNonQuery();
    }

    public void AddWorkLogEntry(WorkLogEntry entry)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO work_log (hardware_id, event_date, event_type, description, cost, status, notes, additional_cost)
            VALUES (@hid, @date, @type, @desc, @cost, @status, @notes, @addcost)
            """;
        cmd.Parameters.AddWithValue("@hid", entry.HardwareId);
        cmd.Parameters.AddWithValue("@date", entry.EventDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@type", entry.EventType);
        cmd.Parameters.AddWithValue("@desc", entry.Description);
        cmd.Parameters.AddWithValue("@cost", (double)entry.Cost);
        cmd.Parameters.AddWithValue("@status", entry.Status);
        cmd.Parameters.AddWithValue("@notes", entry.Notes);
        cmd.Parameters.AddWithValue("@addcost", (double)entry.AdditionalCost);
        cmd.ExecuteNonQuery();
    }

    public void UpdateWorkLogEntry(WorkLogEntry entry)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE work_log SET event_date = @date, event_type = @type, description = @desc,
                cost = @cost, status = @status, notes = @notes, additional_cost = @addcost
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@date", entry.EventDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@type", entry.EventType);
        cmd.Parameters.AddWithValue("@desc", entry.Description);
        cmd.Parameters.AddWithValue("@cost", (double)entry.Cost);
        cmd.Parameters.AddWithValue("@status", entry.Status);
        cmd.Parameters.AddWithValue("@notes", entry.Notes);
        cmd.Parameters.AddWithValue("@addcost", (double)entry.AdditionalCost);
        cmd.ExecuteNonQuery();
    }

    public (int totalCount, decimal totalCost, Dictionary<string, int> byType, Dictionary<string, int> byCondition) GetStats()
    {
        using var conn = GetConnection();

        int totalCount;
        decimal totalCost;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(cost),0) FROM hardware";
            using var r = cmd.ExecuteReader();
            r.Read();
            totalCount = r.GetInt32(0);
            totalCost = Convert.ToDecimal(r.GetDouble(1));
        }

        var byType = new Dictionary<string, int>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT type, COUNT(*) FROM hardware GROUP BY type ORDER BY type";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                byType[r.GetString(0)] = r.GetInt32(1);
        }

        var byCondition = new Dictionary<string, int>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT condition, COUNT(*) FROM hardware GROUP BY condition ORDER BY condition";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                byCondition[r.GetString(0)] = r.GetInt32(1);
        }

        return (totalCount, totalCost, byType, byCondition);
    }

    public decimal GetRepairCostByYear(int year)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(cost),0) FROM work_log WHERE event_type IN ('Ремонт','Замена комплектующего') AND substr(event_date,1,4) = @year";
        cmd.Parameters.AddWithValue("@year", year.ToString());
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    public List<HardwareItem> GetOldEquipment(int years)
    {
        var threshold = DateTime.Today.AddYears(-years).ToString("yyyy-MM-dd");
        var items = new List<HardwareItem>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM hardware WHERE commission_date != '' AND commission_date <= @thr ORDER BY commission_date";
        cmd.Parameters.AddWithValue("@thr", threshold);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(ReadHardware(r));
        return items;
    }

    public void Dispose() { }
}
