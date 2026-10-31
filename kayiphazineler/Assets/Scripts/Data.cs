using System;
using System.Collections.Generic;
using UnityEngine;

namespace KayipHazineler
{
    // Yön bit maskesi: her karo hangi kenarlarının açık olduğunu tutar
    public static class D
    {
        public const int N = 1, E = 2, S = 4, W = 8;
        public static readonly int[] ALL = { N, E, S, W };
        public static int DX(int d) => d == E ? 1 : d == W ? -1 : 0;
        public static int DY(int d) => d == S ? 1 : d == N ? -1 : 0;
        public static int Opp(int d) => d == N ? S : d == S ? N : d == E ? W : E;
        public static int DirOf(int dx, int dy) => dy < 0 ? N : dx > 0 ? E : dy > 0 ? S : W;
    }

    public class MonsterType
    {
        public string name;
        public int hp, atk, move, aggro, xp, gold, score;
        public float size, erratic;
        public bool phase;
    }

    public class ClassDef
    {
        public string id, name, role, perk, weapon;
        public int hp, atk, def, crit, speed;
    }

    public class Gear { public string name; public int stat, cost; public Color color; }
    public class Accessory { public string id, name, desc; public int cost; }
    public class Skill { public string id, name, desc; public int lvl, cd; }

    public class Theme
    {
        public string name;
        public int from;
        public Color floorA, floorB, grout, front, top, hi, moss;
    }

    public static class Data
    {
        public static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly Dictionary<string, MonsterType> Monsters = new Dictionary<string, MonsterType>
        {
            ["goblin"]   = new MonsterType { name = "Yeşil Goblin",     hp = 30,  atk = 8,  move = 430, aggro = 6,  xp = 10,  gold = 5,   score = 5,   size = .95f },
            ["skeleton"] = new MonsterType { name = "İskelet",          hp = 50,  atk = 12, move = 600, aggro = 7,  xp = 18,  gold = 10,  score = 10,  size = 1f },
            ["bat"]      = new MonsterType { name = "Yarasa",           hp = 24,  atk = 7,  move = 280, aggro = 9,  xp = 15,  gold = 15,  score = 15,  size = .95f, erratic = .45f },
            ["spider"]   = new MonsterType { name = "Mağara Sürüngeni", hp = 70,  atk = 18, move = 640, aggro = 6,  xp = 30,  gold = 25,  score = 25,  size = 1.05f },
            ["golem"]    = new MonsterType { name = "Taş Muhafız",      hp = 160, atk = 26, move = 950, aggro = 5,  xp = 60,  gold = 50,  score = 50,  size = 1.1f },
            ["ghost"]    = new MonsterType { name = "Ruh Tuzağı",       hp = 36,  atk = 11, move = 820, aggro = 6,  xp = 22,  gold = 18,  score = 20,  size = .95f, phase = true },
            ["boss"]     = new MonsterType { name = "Minotor",          hp = 420, atk = 30, move = 560, aggro = 99, xp = 200, gold = 150, score = 300, size = 1.45f },
        };

        public static readonly Theme[] Themes =
        {
            new Theme { name = "Kolay Labirent",    from = 1,  floorA = C("#2a332c"), floorB = C("#2e3830"), grout = C("#1a201b"), front = C("#2c3628"), top = C("#55684a"), hi = C("#71885f"), moss = C("#6fae4a") },
            new Theme { name = "Orta Labirent",     from = 11, floorA = C("#222b3d"), floorB = C("#263044"), grout = C("#151b29"), front = C("#252e45"), top = C("#4b5a7e"), hi = C("#64769f"), moss = C("#4aa8ff") },
            new Theme { name = "Zor Labirent",      from = 21, floorA = C("#30201e"), floorB = C("#362422"), grout = C("#1c1312"), front = C("#3a201b"), top = C("#70413a"), hi = C("#8e544a"), moss = C("#ff6a2a") },
            new Theme { name = "Efsanevi Labirent", from = 31, floorA = C("#271e38"), floorB = C("#2c2240"), grout = C("#171123"), front = C("#30254a"), top = C("#5f4b8a"), hi = C("#7b63ae"), moss = C("#c77dff") },
        };
        public static Theme ThemeFor(int floor)
        {
            Theme t = Themes[0];
            foreach (var th in Themes) if (floor >= th.from) t = th;
            return t;
        }

        public static readonly Gear[] Weapons =
        {
            new Gear { name = "Tahta Kılıç",         stat = 0,  cost = 0,    color = C("#b08350") },
            new Gear { name = "Demir Kılıç",         stat = 10, cost = 150,  color = C("#c9d2dc") },
            new Gear { name = "Çelik Kılıç",         stat = 22, cost = 420,  color = C("#eef4fa") },
            new Gear { name = "Ejder Kılıcı",        stat = 38, cost = 950,  color = C("#ff7a4a") },
            new Gear { name = "Kayıp Hazine Kılıcı", stat = 60, cost = 2000, color = C("#f5c04a") },
        };
        public static readonly Gear[] Armors =
        {
            new Gear { name = "Bez Gömlek",      stat = 0,  cost = 0,    color = C("#6e7686") },
            new Gear { name = "Deri Zırh",       stat = 8,  cost = 90,   color = C("#9a6a3c") },
            new Gear { name = "Gümüş Zırh",      stat = 20, cost = 200,  color = C("#cfd6de") },
            new Gear { name = "Ejder Pulu Zırh", stat = 35, cost = 650,  color = C("#e0603a") },
            new Gear { name = "Efsane Zırh",     stat = 55, cost = 1500, color = C("#b98cff") },
        };
        public static readonly Accessory[] Accs =
        {
            new Accessory { id = "ring",  name = "Yüzük",        desc = "+5 Kritik Şansı",            cost = 120 },
            new Accessory { id = "boots", name = "Hız Çizmesi",  desc = "+25 Hareket Hızı",           cost = 140 },
            new Accessory { id = "neck",  name = "Can Kolyesi",  desc = "+30 azami HP",               cost = 160 },
            new Accessory { id = "charm", name = "Altın Tılsım", desc = "Toplanan altın +%25",        cost = 220 },
            new Accessory { id = "fang",  name = "Vampir Dişi",  desc = "Verdiğin hasarın %6'sı HP",  cost = 380 },
        };
        public const string PotionName = "Can İksiri";
        public const int PotionHeal = 50, PotionCost = 80, PotionMax = 5;

        public static readonly Skill[] Skills =
        {
            new Skill { id = "spin",   name = "Hızlı Saldırı", lvl = 1,  cd = 4000,  desc = "Çevrendeki 4 kareye birden vurur" },
            new Skill { id = "jump",   name = "Zıplama",       lvl = 5,  cd = 6000,  desc = "Önündeki duvarın üstünden atlar" },
            new Skill { id = "shield", name = "Kalkan",        lvl = 10, cd = 18000, desc = "4 saniye hasar almazsın" },
        };
        public static Skill SkillById(string id) => Array.Find(Skills, s => s.id == id);

        public static readonly string[] ClassIds = { "knight", "barbarian", "thief", "mage" };
        public static readonly Dictionary<string, ClassDef> Classes = new Dictionary<string, ClassDef>
        {
            ["knight"]    = new ClassDef { id = "knight",    name = "Şövalye", role = "Dengeli savaşçı", hp = 100, atk = 10, def = 4, crit = 5,  speed = 100, weapon = "sword",
                                           perk = "Hızlı Saldırı iki kat sık dolar." },
            ["barbarian"] = new ClassDef { id = "barbarian", name = "Barbar",  role = "Güçlü ama yavaş", hp = 140, atk = 14, def = 0, crit = 5,  speed = 86,  weapon = "axe",
                                           perk = "Öfke: canı %30'un altına düşünce hasarı 1,5 katına çıkar." },
            ["thief"]     = new ClassDef { id = "thief",     name = "Hırsız",  role = "Hızlı ve sinsi",  hp = 80,  atk = 8,  def = 0, crit = 15, speed = 125, weapon = "dagger",
                                           perk = "Kritik vuruşları 2,5 kat vurur, %20 fazla altın toplar." },
            ["mage"]      = new ClassDef { id = "mage",      name = "Büyücü",  role = "Uzaktan büyü",    hp = 75,  atk = 11, def = 0, crit = 5,  speed = 100, weapon = "staff",
                                           perk = "Saldırı tuşu 2 kare uzaktaki canavara büyü fırlatır." },
        };
        public static readonly Dictionary<string, string[]> WeaponNames = new Dictionary<string, string[]>
        {
            ["sword"]  = new[] { "Tahta Kılıç", "Demir Kılıç", "Çelik Kılıç", "Ejder Kılıcı", "Kayıp Hazine Kılıcı" },
            ["axe"]    = new[] { "Odun Baltası", "Demir Balta", "Çelik Balta", "Ejder Baltası", "Kayıp Hazine Baltası" },
            ["dagger"] = new[] { "Tahta Hançer", "Demir Hançer", "Çelik Hançer", "Ejder Dişi Hançer", "Kayıp Hazine Hançeri" },
            ["staff"]  = new[] { "Meşe Asa", "Kristal Asa", "Yıldız Asası", "Ejder Asası", "Kayıp Hazine Asası" },
        };
        public static readonly Color[] StaffOrbs = { C("#9ad0ff"), C("#5fd0ff"), C("#ffe14a"), C("#ff7a4a"), C("#f5c04a") };

        public static readonly string[] Names = { "Deniz", "Ege", "Ada", "Toprak", "Rüzgar", "Yağmur", "Ateş", "Nehir", "Umut", "Bulut", "Ilgaz", "Kuzey", "Derin", "Evren", "Güneş", "Çağan" };

        // Döner koridor ve fener süreleri (ms)
        public const int RotMs = 4200, LampMs = 6000, LampCd = 15000;
        public static int XpNeed(int l) => 30 * l + 6 * l * l;
    }

    // Kayıtlı oyun: kat aralarında ve menüye dönerken PlayerPrefs'e yazılır
    [Serializable]
    public class RunData
    {
        public string hero = "knight", name = "";
        public int floor = 1, gold, score, kills, lvl = 1, xp, hp, weapon, armor, potions = 1;
        public List<string> acc = new List<string>();
        public bool Has(string id) => acc.Contains(id);

        public static RunData Fresh(string hero)
        {
            if (!Data.Classes.ContainsKey(hero)) hero = "knight";
            return new RunData { hero = hero, hp = Data.Classes[hero].hp };
        }
    }

    [Serializable]
    public class ScoreEntry { public string name, hero; public int floor, lvl, kills, score; public long t; }

    [Serializable]
    public class ScoreList { public List<ScoreEntry> items = new List<ScoreEntry>(); }

    public static class Save
    {
        const string RunKey = "kayip_run", ScoreKey = "kayip_scores", AudioKey = "kayip_audio";
        public const int TopN = 10;

        public static void SaveRun(RunData r) { PlayerPrefs.SetString(RunKey, JsonUtility.ToJson(r)); PlayerPrefs.Save(); }
        public static RunData LoadRun()
        {
            try
            {
                var r = JsonUtility.FromJson<RunData>(PlayerPrefs.GetString(RunKey, ""));
                return r != null && r.floor > 0 ? r : null;
            }
            catch { return null; }
        }
        public static void ClearRun() { PlayerPrefs.DeleteKey(RunKey); PlayerPrefs.Save(); }

        public static List<ScoreEntry> LoadScores()
        {
            try
            {
                var l = JsonUtility.FromJson<ScoreList>(PlayerPrefs.GetString(ScoreKey, ""));
                if (l != null && l.items != null) return l.items;
            }
            catch { }
            return new List<ScoreEntry>();
        }
        // Rekor tablosu: biten oyunların en iyi 10'u bu cihazda saklanır; dönen değer girişin sırasıdır
        public static int AddScore(ScoreEntry e)
        {
            var a = LoadScores();
            a.Add(e);
            a.Sort((x, y) => y.score != x.score ? y.score - x.score : y.floor != x.floor ? y.floor - x.floor : x.t.CompareTo(y.t));
            if (a.Count > TopN) a.RemoveRange(TopN, a.Count - TopN);
            PlayerPrefs.SetString(ScoreKey, JsonUtility.ToJson(new ScoreList { items = a }));
            PlayerPrefs.Save();
            return a.IndexOf(e);
        }

        public static bool AudioOn
        {
            get => PlayerPrefs.GetInt(AudioKey, 1) == 1;
            set { PlayerPrefs.SetInt(AudioKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
