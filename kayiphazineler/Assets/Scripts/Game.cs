using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KayipHazineler
{
    public class Ent
    {
        public int x, y;
        public float fx, fy, t = 1, dur = 170;
        public void Snap() { fx = x; fy = y; t = 1; }
        public void MoveTo(int nx, int ny, float d) { fx = x; fy = y; x = nx; y = ny; t = 0; dur = d; }
        // Yumuşak geçişli konum (karo biriminde)
        public Vector2 Pos()
        {
            float k = t >= 1 ? 1 : t * (2 - t);
            return new Vector2(fx + (x - fx) * k, fy + (y - fy) * k);
        }
    }

    public class Player : Ent
    {
        public int face = D.S, side = 1;
        public float atkCd, lunge, slash, hurt, spinFx, shield, lamp;
        public bool jumping;
        public readonly Dictionary<string, float> cd = new Dictionary<string, float> { ["spin"] = 0, ["jump"] = 0, ["shield"] = 0, ["potion"] = 0, ["lamp"] = 0 };
    }

    public class Monster : Ent
    {
        public string type;
        public int hp, maxHp, atk, gold, xp, score, move, aggro, face = D.S;
        public float erratic, timer, atkCd, hurt, lunge, seed, size;
        public bool phase, key;
    }

    public class Coin : Ent { public int v; }
    public class Item : Ent { public string type; public bool big; }
    public class Feat { public int slide; public bool rot; }
    public class ShiftLine { public bool row; public int idx, dir; public float t; }
    public class Floater { public string text; public Vector2 pos; public Color color; public bool big; public float life = 1; }
    public class Particle { public Vector2 pos, vel; public Color color; public float life = 1, ttl, size; public bool floaty; }
    public class Bolt { public Vector2 a, b; public float t; public Monster target; public bool done; }

    public enum State { Title, Select, Play, Pause, Shop, Over, Scores }

    // Oyunun kuralları: labirent, kayma, canavarlar, savaş ve ilerleme
    public partial class Game : MonoBehaviour
    {
        public State state = State.Title;
        public RunData R;
        public int w, h;
        public int[,] tiles;    // [y, x] açık kenar maskesi
        public float[,] seeds;
        public Feat[,] feat;
        public Player P;
        public List<Monster> monsters = new List<Monster>();
        public List<Coin> coins = new List<Coin>();
        public List<Item> items = new List<Item>();
        public List<Floater> floaters = new List<Floater>();
        public List<Particle> parts = new List<Particle>();
        public List<Bolt> bolts = new List<Bolt>();
        public Ent exit, key, secret;
        public bool hasKey, dirty;
        public float shake, rotTimer, shiftTimer, shiftTotal, grace;
        public ShiftLine warn, anim;
        public int lastBonus, lastRank = -1;
        public bool preview;

        // Girdi: basılı tutulan ve sıraya alınan yön
        int held, queued;
        readonly List<int> keysDown = new List<int>();

        static float Rnd(float a, float b) => a + Random.value * (b - a);
        static int Ri(int a, int b) => Random.Range(a, b + 1);
        static T Pick<T>(IList<T> a) => a[Random.Range(0, a.Count)];

        public ClassDef Cls => Data.Classes.TryGetValue(R.hero, out var c) ? c : Data.Classes["knight"];
        public string WeaponName(int i) => Data.WeaponNames[Cls.weapon][i];
        public Color WeaponColor => Cls.weapon == "staff" ? Data.StaffOrbs[R.weapon] : Data.Weapons[R.weapon].color;
        public int MaxHp => Cls.hp + (R.lvl - 1) * 10 + (R.Has("neck") ? 30 : 0);
        public bool Raging => R.hero == "barbarian" && R.hp < MaxHp * .3f;
        public int AtkPow => Mathf.RoundToInt((Cls.atk + (R.lvl - 1) * 2 + Data.Weapons[R.weapon].stat) * (Raging ? 1.5f : 1));
        public int DefPow => Data.Armors[R.armor].stat + (R.lvl - 1) + Cls.def;
        public int CritPct => Cls.crit + (R.Has("ring") ? 5 : 0);
        public int SpeedStat => Cls.speed + (R.Has("boots") ? 25 : 0) + (R.lvl - 1) * 2;
        public float GoldMul => (R.Has("charm") ? 1.25f : 1) * (R.hero == "thief" ? 1.2f : 1);
        public int SkillCd(string id) => id == "spin" && R.hero == "knight" ? 2000 : Data.SkillById(id).cd;
        public bool SkillOpen(string id) => R.lvl >= Data.SkillById(id).lvl;
        public string HeroName => string.IsNullOrWhiteSpace(R.name) ? Cls.name : R.name;
        public Theme Theme => Data.ThemeFor(R.floor);
        float MoveMs => Mathf.Round(17000f / SpeedStat);
        // Kayma süresi her seferinde rastgele: ortalaması katla kısalır, en az 3 sn (uyarı 1,7 sn sürer)
        float ShiftInterval()
        {
            float b = Mathf.Max(5000, 11000 - (R.floor - 1) * 400);
            return Mathf.Round(Mathf.Max(3000, Rnd(b * .4f, b * 1.5f)));
        }

        /* ---------- Labirent ---------- */
        int[,] GenMaze(int mw, int mh)
        {
            var t = new int[mh, mw];
            var seen = new bool[mh, mw];
            var st = new Stack<Vector2Int>();
            var s0 = new Vector2Int(Ri(0, mw - 1), Ri(0, mh - 1));
            st.Push(s0); seen[s0.y, s0.x] = true;
            var opts = new List<int>(4);
            while (st.Count > 0)
            {
                var c = st.Peek();
                opts.Clear();
                foreach (var d in D.ALL)
                {
                    int nx = c.x + D.DX(d), ny = c.y + D.DY(d);
                    if (nx >= 0 && ny >= 0 && nx < mw && ny < mh && !seen[ny, nx]) opts.Add(d);
                }
                if (opts.Count == 0) { st.Pop(); continue; }
                int dd = Pick(opts), ax = c.x + D.DX(dd), ay = c.y + D.DY(dd);
                t[c.y, c.x] |= dd; t[ay, ax] |= D.Opp(dd); seen[ay, ax] = true;
                st.Push(new Vector2Int(ax, ay));
            }
            // Döngüler ekle: kayma sonrası çıkmazlar daha az olsun
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                    if (Random.value < .3f)
                    {
                        int d = Pick(D.ALL), nx = x + D.DX(d), ny = y + D.DY(d);
                        if (nx >= 0 && ny >= 0 && nx < mw && ny < mh) { t[y, x] |= d; t[ny, nx] |= D.Opp(d); }
                    }
            return t;
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        public bool CanPass(int x, int y, int d)
        {
            int nx = x + D.DX(d), ny = y + D.DY(d);
            if (!Inside(nx, ny)) return false;
            return (tiles[y, x] & d) != 0 && (tiles[ny, nx] & D.Opp(d)) != 0;
        }
        void OpenWall(int x, int y, int d)
        {
            tiles[y, x] |= d;
            tiles[y + D.DY(d), x + D.DX(d)] |= D.Opp(d);
            dirty = true;
        }
        int[,] Bfs(int sx, int sy)
        {
            var dist = new int[h, w];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) dist[y, x] = -1;
            dist[sy, sx] = 0;
            var q = new Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(sx, sy));
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in D.ALL)
                    if (CanPass(c.x, c.y, d))
                    {
                        int nx = c.x + D.DX(d), ny = c.y + D.DY(d);
                        if (dist[ny, nx] < 0) { dist[ny, nx] = dist[c.y, c.x] + 1; q.Enqueue(new Vector2Int(nx, ny)); }
                    }
            }
            return dist;
        }
        // Kayma oyuncuyu kapatırsa yeterince alan açılana kadar duvar yıkar
        void EnsureReach(float frac)
        {
            int need = Mathf.CeilToInt(w * h * frac);
            var front = new List<Vector3Int>();
            for (int guard = 0; guard < 300; guard++)
            {
                var dist = Bfs(P.x, P.y);
                int cnt = 0; front.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (dist[y, x] >= 0)
                        {
                            cnt++;
                            foreach (var d in D.ALL)
                            {
                                int nx = x + D.DX(d), ny = y + D.DY(d);
                                if (Inside(nx, ny) && dist[ny, nx] < 0 && !IsSecret(nx, ny)) front.Add(new Vector3Int(x, y, d));
                            }
                        }
                Ent goal = null;
                if (key != null && dist[key.y, key.x] < 0) goal = key;
                else if (exit != null && dist[exit.y, exit.x] < 0) goal = exit;
                if (cnt >= need && goal == null) return;
                if (front.Count == 0) return;
                Vector3Int f;
                if (cnt >= need)
                {
                    int Md(Vector3Int a) => Mathf.Abs(a.x + D.DX(a.z) - goal.x) + Mathf.Abs(a.y + D.DY(a.z) - goal.y);
                    f = front.Aggregate((a, b) => Md(a) <= Md(b) ? a : b);
                }
                else f = Pick(front);
                OpenWall(f.x, f.y, f.z);
                Dust(f.x + .5f + D.DX(f.z) * .5f, f.y + .5f + D.DY(f.z) * .5f, Theme.hi, 8);
            }
        }

        public bool IsSecret(int x, int y) => secret != null && secret.x == x && secret.y == y;
        // Gizli bölme başka bir yoldan açıldıysa artık gizli değildir
        void CheckSecret()
        {
            if (secret != null && D.ALL.Any(d => CanPass(secret.x, secret.y, d))) secret = null;
        }
        // Döner koridor: karonun açıklıklarını değiştirir, komşu duvarları da ona uydurur
        void SetMask(int x, int y, int m)
        {
            tiles[y, x] = m;
            foreach (var d in D.ALL)
            {
                int nx = x + D.DX(d), ny = y + D.DY(d);
                if (!Inside(nx, ny)) continue;
                if ((m & d) != 0) tiles[ny, nx] |= D.Opp(d); else tiles[ny, nx] &= ~D.Opp(d);
            }
            dirty = true;
        }
        void TurnCorridors()
        {
            bool any = false;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (feat[y, x] != null && feat[y, x].rot)
                    {
                        int t = tiles[y, x];
                        SetMask(x, y, ((t << 1) | (t >> 3)) & 15);
                        any = true;
                    }
            if (!any) return;
            Sfx.Play("turn");
            EnsureReach(.65f); CheckSecret();
        }

        /* ---------- Oyun akışı ---------- */
        public void NewRun(RunData data)
        {
            R = data ?? RunData.Fresh("knight");
            if (!Data.Classes.ContainsKey(R.hero)) R.hero = "knight";
            if (R.acc == null) R.acc = new List<string>();
            if (R.hp <= 0) R.hp = MaxHp;
            StartLevel(false);
        }

        public void StartLevel(bool isPreview)
        {
            preview = isPreview;
            int F = R.floor;
            w = Mathf.Min(13, 8 + (F - 1) / 3);
            // Satır sayısı oyun alanının en-boy oranına göre: telefonda dikey labirent
            float aspect = BoardAspect();
            h = Mathf.Max(w, Mathf.Min(Mathf.RoundToInt(w * 2f), Mathf.FloorToInt(w * aspect)));
            tiles = GenMaze(w, h);
            seeds = new float[h, w];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) seeds[y, x] = Random.value;
            feat = new Feat[h, w];
            int px = Ri(0, w - 1), py = Ri(0, h - 1);
            P = new Player { x = px, y = py, fx = px, fy = py };
            monsters.Clear(); coins.Clear(); items.Clear(); bolts.Clear(); floaters.Clear(); parts.Clear();
            exit = key = secret = null; hasKey = false; shake = 0; dirty = true; warn = anim = null;
            bool boss = F % 5 == 0;

            bool At(Ent e, int x, int y) => e != null && e.x == x && e.y == y;
            bool Reserved(int x, int y) => At(exit, x, y) || At(key, x, y) || IsSecret(x, y) || items.Any(i => At(i, x, y));
            Ent Best(Func<int, int, float> score)
            {
                Ent b = null; float bs = float.NegativeInfinity;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float v = score(x, y);
                        if (v > bs && !(x == px && y == py) && !Reserved(x, y)) { bs = v; b = new Ent { x = x, y = y, fx = x, fy = y }; }
                    }
                return b;
            }
            // Çıkış kapısı başlangıca en uzak karoda; anahtar hem başlangıçtan hem kapıdan uzakta
            var ds = Bfs(px, py);
            exit = Best((x, y) => ds[y, x]);
            var de = Bfs(exit.x, exit.y);
            void PlaceKey() { key = Best((x, y) => Mathf.Min(ds[y, x], de[y, x]) + Random.value); }
            if (!boss) PlaceKey();

            // Gizli bölme: bir çıkmaz sokağın dört duvarı da örülür, içinde büyük hazine saklanır
            var ends = new List<Vector2Int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (ds[y, x] >= 3 && !Reserved(x, y) && D.ALL.Count(d => CanPass(x, y, d)) == 1) ends.Add(new Vector2Int(x, y));
            if (ends.Count > 0)
            {
                var s = Pick(ends);
                foreach (var d in D.ALL)
                    if (CanPass(s.x, s.y, d)) { tiles[s.y, s.x] &= ~d; tiles[s.y + D.DY(d), s.x + D.DX(d)] &= ~D.Opp(d); }
                secret = new Ent { x = s.x, y = s.y, fx = s.x, fy = s.y };
                items.Add(new Item { x = s.x, y = s.y, fx = s.x, fy = s.y, type = "chest", big = true });
            }

            Vector2Int? FreeCell(int minD)
            {
                for (int k = 0; k < 500; k++)
                {
                    int x = Ri(0, w - 1), y = Ri(0, h - 1);
                    if (Mathf.Abs(x - px) + Mathf.Abs(y - py) >= minD && !Occupied(x, y) && !Reserved(x, y)) return new Vector2Int(x, y);
                }
                return null;
            }
            // Saklı ödüller: hazine sandığı, değerli taş ve altın keseleri
            var loot = new List<string> { "chest", "gem" };
            for (int i = 0; i < 1 + w * h / 60; i++) loot.Add("bag");
            foreach (var type in loot)
            {
                var c = FreeCell(4);
                if (c.HasValue) items.Add(new Item { x = c.Value.x, y = c.Value.y, fx = c.Value.x, fy = c.Value.y, type = type });
            }

            // Zemin mekanikleri: kayar zemin oklar yönünde iter, döner koridor yön değiştirir
            void PutFeat(int n, Func<int, int, bool> test, Func<int, int, Feat> make)
            {
                for (int i = 0, k = 0; i < n && k < 400; k++)
                {
                    int x = Ri(0, w - 1), y = Ri(0, h - 1);
                    if (feat[y, x] != null || (x == px && y == py) || Reserved(x, y) || !test(x, y)) continue;
                    feat[y, x] = make(x, y); i++;
                }
            }
            PutFeat(Mathf.Min(6, 2 + F / 3), (x, y) => D.ALL.Any(d => CanPass(x, y, d)),
                (x, y) => new Feat { slide = Pick(D.ALL.Where(d => CanPass(x, y, d)).ToList()) });
            bool NearBy(int x, int y, Func<int, int, bool> fn) => D.ALL.Any(d =>
            {
                int nx = x + D.DX(d), ny = y + D.DY(d);
                return Inside(nx, ny) && fn(nx, ny);
            });
            if (F >= 2)
                PutFeat(Mathf.Min(3, 1 + (F - 2) / 4),
                    (x, y) => x > 0 && y > 0 && x < w - 1 && y < h - 1 && !NearBy(x, y, (nx, ny) => IsSecret(nx, ny) || (feat[ny, nx] != null && feat[ny, nx].rot)),
                    (x, y) => { SetMask(x, y, Random.value < .5f ? D.N | D.S : D.E | D.W); return new Feat { rot = true }; });
            EnsureReach(.9f);

            var pool = new List<string> { "goblin", "goblin", "goblin" };
            if (F >= 2) pool.AddRange(new[] { "skeleton", "skeleton" });
            if (F >= 3) pool.Add("bat");
            if (F >= 4) pool.Add("ghost");
            if (F >= 5) pool.AddRange(new[] { "spider", "spider" });
            if (F >= 8) pool.Add("golem");
            int count = Mathf.Min(Mathf.FloorToInt((3 + F * 1.3f) * w * h / 100f + 1), w * h / 6);
            // Minotor katında anahtar Minotordadır
            if (boss)
            {
                var c = FreeCell(6);
                if (c.HasValue) { var b = MakeMonster("boss", c.Value.x, c.Value.y); b.key = true; monsters.Add(b); }
                else PlaceKey();
            }
            while (monsters.Count < count + (boss ? 1 : 0))
            {
                string type = Pick(pool);
                var c = FreeCell(4);
                if (!c.HasValue) break;
                monsters.Add(MakeMonster(type, c.Value.x, c.Value.y));
                // Yarasalar sürü halinde gelir
                if (type == "bat")
                    foreach (var d in D.ALL)
                    {
                        int bx = c.Value.x + D.DX(d), by = c.Value.y + D.DY(d);
                        if (Inside(bx, by) && !Occupied(bx, by) && !Reserved(bx, by) && Random.value < .5f) monsters.Add(MakeMonster("bat", bx, by));
                    }
            }
            // Anahtarı bir muhafız bekler
            if (key != null && F >= 3)
            {
                foreach (var d in D.ALL)
                    if (CanPass(key.x, key.y, d) && !Occupied(key.x + D.DX(d), key.y + D.DY(d)))
                    {
                        monsters.Add(MakeMonster(F >= 8 ? "golem" : "spider", key.x + D.DX(d), key.y + D.DY(d)));
                        break;
                    }
            }
            for (int i = 0; i < w * h / 10; i++)
            {
                var c = FreeCell(1);
                if (c.HasValue && !coins.Any(k => k.x == c.Value.x && k.y == c.Value.y))
                    coins.Add(new Coin { x = c.Value.x, y = c.Value.y, fx = c.Value.x, fy = c.Value.y, v = Ri(2, 4) });
            }
            rotTimer = Data.RotMs;
            shiftTotal = shiftTimer = ShiftInterval();
            grace = 1500;
            R.hp = Mathf.Min(R.hp, MaxHp);
            if (isPreview) return;
            state = State.Play;
            Save.SaveRun(R);
            Toast(F == 1 ? "Anahtarı bul, çıkışa ulaş!" : boss ? $"Kat {F} · Anahtar Minotorda!" : $"Kat {F} · {Theme.name}");
        }

        Monster MakeMonster(string type, int x, int y)
        {
            var b = Data.Monsters[type];
            int F = R.floor, hp = Mathf.RoundToInt(b.hp * (1 + .18f * (F - 1)));
            return new Monster
            {
                type = type, x = x, y = y, fx = x, fy = y, dur = Mathf.Min(260, b.move * .6f), hp = hp, maxHp = hp,
                atk = Mathf.RoundToInt(b.atk * (1 + .12f * (F - 1))), move = b.move, aggro = b.aggro, erratic = b.erratic,
                gold = Mathf.RoundToInt(b.gold * (1 + .1f * (F - 1))), xp = Mathf.RoundToInt(b.xp * (1 + .1f * (F - 1))), score = b.score,
                timer = Rnd(400, b.move + 800), atkCd = 800, phase = b.phase, seed = Random.value * 10, size = b.size,
            };
        }
        public Monster MonsterAt(int x, int y) => monsters.Find(m => m.x == x && m.y == y);
        bool Occupied(int x, int y) => (P.x == x && P.y == y) || MonsterAt(x, y) != null;

        void Face(int d) { P.face = d; if (d == D.E) P.side = 1; else if (d == D.W) P.side = -1; }
        bool TryMove(int d)
        {
            Face(d);
            if (!CanPass(P.x, P.y, d)) return true;
            int nx = P.x + D.DX(d), ny = P.y + D.DY(d);
            var m = MonsterAt(nx, ny);
            if (m != null) { if (P.atkCd > 0) return false; Swing(m); return true; }
            P.MoveTo(nx, ny, MoveMs); P.jumping = false;
            Sfx.Play("step");
            return true;
        }
        void Swing(Monster target)
        {
            P.atkCd = 300; P.lunge = 1; P.slash = 1; Sfx.Play("swing");
            if (target != null) HitMonster(target, AtkPow);
        }
        public void Attack()
        {
            if (state != State.Play || anim != null) return;
            if (P.atkCd > 0 || P.t < 1) return;
            int d = P.face, nx = P.x + D.DX(d), ny = P.y + D.DY(d);
            bool open = CanPass(P.x, P.y, d);
            var fm = MonsterAt(nx, ny);
            var m = fm != null && (open || fm.phase) ? fm : null;
            // Gizli bölmenin duvarına vurmak onu yıkar
            if (!open && IsSecret(nx, ny)) RevealSecret(d);
            if (R.hero == "mage") CastBolt(d, m); else Swing(m);
        }
        void RevealSecret(int d)
        {
            OpenWall(P.x, P.y, d); secret = null;
            Dust(P.x + D.DX(d) + .5f, P.y + D.DY(d) + .5f, Data.C("#c77dff"), 22);
            shake = 5;
            Toast("Gizli bölme bulundu!"); Sfx.Play("portal");
        }
        public void UseLamp()
        {
            if (state != State.Play || P.cd["lamp"] > 0) return;
            P.lamp = Data.LampMs; P.cd["lamp"] = Data.LampCd; Sfx.Play("lamp");
            if (secret != null && Mathf.Abs(secret.x - P.x) + Mathf.Abs(secret.y - P.y) <= 4) Toast("Yakında gizli bir bölme var!");
        }
        // Büyücü: büyü 2 kareye kadar ilerler, duvarda ya da ilk canavarda durur
        void CastBolt(int d, Monster near)
        {
            P.atkCd = 380; P.lunge = .6f; P.slash = .8f; Sfx.Play("bolt");
            int x = P.x, y = P.y; var target = near;
            if (near != null) { x = near.x; y = near.y; }
            else
                for (int i = 0; i < 2; i++)
                {
                    if (!CanPass(x, y, d)) break;
                    x += D.DX(d); y += D.DY(d);
                    var m = MonsterAt(x, y);
                    if (m != null) { target = m; break; }
                }
            bool self = x == P.x && y == P.y;
            var end = self ? new Vector2(P.x + .5f + D.DX(d) * .45f, P.y + .3f + D.DY(d) * .45f) : new Vector2(x + .5f, y + .3f);
            bolts.Add(new Bolt { a = new Vector2(P.x + .5f, P.y + .3f), b = end, target = target });
        }
        public void UseSkill(string id)
        {
            if (state != State.Play || anim != null) return;
            var sk = Data.SkillById(id);
            if (!SkillOpen(id)) { Toast($"{sk.name} Lv{sk.lvl}'de açılır"); return; }
            if (P.cd[id] > 0) return;
            if (id == "spin")
            {
                P.spinFx = 1; Sfx.Play("spin");
                foreach (var m in D.ALL.Select(d => MonsterAt(P.x + D.DX(d), P.y + D.DY(d))).Where(m => m != null).ToList())
                    HitMonster(m, Mathf.RoundToInt(AtkPow * 1.5f));
            }
            else if (id == "jump")
            {
                if (P.t < 1) return;
                int nx = P.x + D.DX(P.face), ny = P.y + D.DY(P.face);
                if (!Inside(nx, ny) || Occupied(nx, ny)) { Toast("Atlanacak yer yok"); return; }
                P.MoveTo(nx, ny, 340); P.jumping = true;
                Sfx.Play("jump");
            }
            else if (id == "shield")
            {
                P.shield = 4000; Sfx.Play("shield");
            }
            P.cd[id] = SkillCd(id);
        }
        public void DrinkPotion()
        {
            if (state != State.Play) return;
            if (R.potions <= 0) { Toast("İksirin kalmadı"); return; }
            if (R.hp >= MaxHp || P.cd["potion"] > 0) return;
            R.potions--; P.cd["potion"] = 800;
            int heal = Mathf.Min(Data.PotionHeal, MaxHp - R.hp);
            R.hp += heal;
            Floater("+" + heal + " HP", P.x + .5f, P.y + .1f, Data.C("#5fcf7a"));
            Dust(P.x + .5f, P.y + .5f, Data.C("#e0413f"), 10); Sfx.Play("potion");
        }
        void HitMonster(Monster m, float dmg)
        {
            bool crit = Random.value * 100 < CritPct;
            if (crit) dmg *= R.hero == "thief" ? 2.5f : 2f;
            int dm = Mathf.RoundToInt(dmg * Rnd(.9f, 1.1f));
            m.hp -= dm; m.hurt = 1;
            Floater("-" + dm, m.x + .5f, m.y + .15f, crit ? Data.C("#ffb02a") : Data.C("#ff4a4a"), crit);
            if (crit) Sfx.Play("crit");
            Dust(m.x + .5f, m.y + .5f, Color.white, 4);
            Sfx.Play("hit");
            if (R.Has("fang")) R.hp = Mathf.Min(MaxHp, R.hp + Mathf.Max(1, Mathf.RoundToInt(dm * .06f)));
            if (m.hp <= 0) KillMonster(m);
        }
        void KillMonster(Monster m)
        {
            monsters.Remove(m);
            R.score += m.score; R.kills++;
            Dust(m.x + .5f, m.y + .5f, Data.C("#c9c2b0"), 18);
            var c = coins.Find(k => k.x == m.x && k.y == m.y);
            if (c != null) c.v += m.gold; else coins.Add(new Coin { x = m.x, y = m.y, fx = m.x, fy = m.y, v = m.gold });
            Sfx.Play("kill");
            GainXp(m.xp, m.x + .5f, m.y - .1f);
            if (m.key) { key = new Ent { x = m.x, y = m.y, fx = m.x, fy = m.y }; Toast("Anahtar düştü!"); Sfx.Play("portal"); }
        }
        void GainXp(int v, float x, float y)
        {
            R.xp += v;
            Floater("+" + v + " XP", x, y, Data.C("#7ab4ff"));
            while (R.xp >= Data.XpNeed(R.lvl))
            {
                R.xp -= Data.XpNeed(R.lvl); R.lvl++;
                R.hp = MaxHp;
                var sk = Array.Find(Data.Skills, s => s.lvl == R.lvl);
                Toast(sk != null ? $"{HeroName} Lv. {R.lvl}! {sk.name} açıldı" : $"{HeroName} seviye atladı! Lv. {R.lvl}");
                Dust(P.x + .5f, P.y + .5f, Data.C("#f5c04a"), 24); Sfx.Play("level");
            }
        }
        void HurtPlayer(int raw)
        {
            if (P.shield > 0) { Floater("Engellendi", P.x + .5f, P.y + .1f, Data.C("#7ab4ff")); Sfx.Play("block"); return; }
            int dmg = Mathf.Max(1, Mathf.RoundToInt(raw * 50f / (50 + DefPow)));
            R.hp -= dmg; P.hurt = 1; shake = 8;
            Floater("-" + dmg, P.x + .5f, P.y + .1f, Data.C("#ff4a4a"));
            Sfx.Play("hurt");
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            if (R.hp <= 0) { R.hp = 0; GameOver(); }
        }
        void OnArrive()
        {
            P.jumping = false;
            int ci = coins.FindIndex(c => c.x == P.x && c.y == P.y);
            if (ci >= 0)
            {
                var c = coins[ci]; coins.RemoveAt(ci);
                int v = Mathf.RoundToInt(c.v * GoldMul);
                R.gold += v;
                Floater("+" + v, P.x + .5f, P.y + .2f, Data.C("#f5c04a"));
                Sfx.Play("coin");
            }
            int ii = items.FindIndex(i => i.x == P.x && i.y == P.y);
            if (ii >= 0) { var it = items[ii]; items.RemoveAt(ii); Collect(it); }
            if (key != null && key.x == P.x && key.y == P.y)
            {
                key = null; hasKey = true; R.score += 15;
                Toast("Gizemli anahtar! Çıkış açıldı");
                Dust(P.x + .5f, P.y + .5f, Data.C("#f5c04a"), 20); Sfx.Play("key");
            }
            if (exit != null && exit.x == P.x && exit.y == P.y)
            {
                if (hasKey) { LevelComplete(); return; }
                Toast(key != null ? "Kapı kilitli · anahtarı bul" : "Kapı kilitli · anahtar Minotorda");
            }
            // Kayar zemin: oklar yönünde bir kare daha kaydırır
            var f = feat[P.y, P.x];
            if (f != null && f.slide != 0 && CanPass(P.x, P.y, f.slide))
            {
                int nx = P.x + D.DX(f.slide), ny = P.y + D.DY(f.slide);
                if (!Occupied(nx, ny)) { P.MoveTo(nx, ny, Mathf.Round(MoveMs * .55f)); Sfx.Play("slide"); }
            }
        }
        void Collect(Item it)
        {
            int F = R.floor;
            if (it.type == "chest")
            {
                int v = Mathf.RoundToInt(Ri(25, 40) * (1 + .15f * (F - 1)) * GoldMul * (it.big ? 2 : 1));
                R.gold += v; R.score += it.big ? 60 : 20;
                Toast(it.big ? $"Gizli hazine! +{v} altın" : $"Hazine sandığı! +{v} altın");
                if (it.big && R.potions < Data.PotionMax) { R.potions++; Floater("+1 İksir", P.x + .5f, P.y - .2f, Data.C("#ff8a8a")); }
                Dust(P.x + .5f, P.y + .5f, Data.C("#f5c04a"), 26); Sfx.Play("chest");
            }
            else if (it.type == "gem")
            {
                int pts = 40 + 5 * F, v = Mathf.RoundToInt(15 * GoldMul);
                R.score += pts; R.gold += v;
                Toast($"Değerli taş! +{pts} puan");
                Dust(P.x + .5f, P.y + .5f, Data.C("#c77dff"), 22); Sfx.Play("chest");
            }
            else if (it.type == "bag")
            {
                int v = Mathf.RoundToInt(Ri(12, 22) * (1 + .1f * (F - 1)) * GoldMul);
                R.gold += v;
                Floater("+" + v, P.x + .5f, P.y + .2f, Data.C("#f5c04a"), true);
                Dust(P.x + .5f, P.y + .5f, Data.C("#f5c04a"), 12); Sfx.Play("coin");
            }
        }
        void LevelComplete()
        {
            lastBonus = 50 * R.floor;
            R.score += lastBonus;
            R.hp = Mathf.Min(MaxHp, R.hp + Mathf.CeilToInt(MaxHp * .2f));
            state = State.Shop;
            Sfx.Play("portal");
            Save.SaveRun(R);
        }
        public void NextFloor()
        {
            R.floor++;
            StartLevel(false);
        }
        void GameOver()
        {
            state = State.Over;
            var e = new ScoreEntry { name = HeroName, hero = R.hero, floor = R.floor, lvl = R.lvl, kills = R.kills, score = R.score, t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            lastRank = R.score > 0 ? Save.AddScore(e) : -1;
            Sfx.Play("gameover");
            Save.ClearRun();
        }

        // Mağazadan alışveriş: anahtar "weapon:2", "armor:1", "acc:ring" ya da "potion"
        public void Buy(string what)
        {
            var parts = what.Split(':');
            string kind = parts[0];
            int cost;
            if (kind == "weapon") { int i = int.Parse(parts[1]); cost = Data.Weapons[i].cost; if (R.gold < cost || R.weapon >= i) return; R.weapon = i; }
            else if (kind == "armor") { int i = int.Parse(parts[1]); cost = Data.Armors[i].cost; if (R.gold < cost || R.armor >= i) return; R.armor = i; }
            else if (kind == "acc")
            {
                var a = Array.Find(Data.Accs, x => x.id == parts[1]); cost = a.cost;
                if (R.gold < cost || R.Has(a.id)) return;
                R.acc.Add(a.id);
                if (a.id == "neck") R.hp += 30;
            }
            else { cost = Data.PotionCost; if (R.gold < cost || R.potions >= Data.PotionMax) return; R.potions++; }
            R.gold -= cost;
            Sfx.Play("buy");
            Save.SaveRun(R);
        }

        /* ---------- Kayma ---------- */
        ShiftLine PickLine()
        {
            bool row = Random.value < .5f;
            int near = row ? P.y : P.x, len = row ? h : w;
            int idx = Random.value < .55f ? near + Ri(-1, 1) : Ri(0, len - 1);
            return new ShiftLine { row = row, idx = Mathf.Clamp(idx, 0, len - 1), dir = Random.value < .5f ? 1 : -1 };
        }
        void Rotate<T>(T[,] grid, ShiftLine a)
        {
            int d = a.dir;
            if (a.row)
            {
                var row = new T[w];
                for (int x = 0; x < w; x++) row[x] = grid[a.idx, x];
                for (int x = 0; x < w; x++) grid[a.idx, (x + d + w) % w] = row[x];
            }
            else
            {
                var col = new T[h];
                for (int y = 0; y < h; y++) col[y] = grid[y, a.idx];
                for (int y = 0; y < h; y++) grid[(y + d + h) % h, a.idx] = col[y];
            }
        }
        void CommitShift(ShiftLine a)
        {
            int d = a.dir;
            Rotate(tiles, a); Rotate(seeds, a); Rotate(feat, a);
            void Mv(Ent e)
            {
                if (e == null) return;
                if (a.row && e.y == a.idx) e.x = (e.x + d + w) % w;
                else if (!a.row && e.x == a.idx) e.y = (e.y + d + h) % h;
                e.Snap();
            }
            Mv(P);
            monsters.ForEach(Mv); coins.ForEach(Mv); items.ForEach(Mv);
            Mv(exit); Mv(key); Mv(secret);
            dirty = true;
            EnsureReach(.65f); CheckSecret();
        }

        /* ---------- Güncelleme ---------- */
        void Tick(float dt)
        {
            UpdateFx(dt);
            if (state != State.Play) return;
            if (anim != null)
            {
                anim.t += dt / 420;
                if (anim.t >= 1) { CommitShift(anim); anim = null; }
                return;
            }
            shiftTimer -= dt;
            if (warn == null && shiftTimer <= 1700) { warn = PickLine(); Sfx.Play("rumble"); }
            if (shiftTimer <= 0)
            {
                float pt = P.t;
                P.Snap(); monsters.ForEach(m => m.Snap());
                if (pt < 1) OnArrive();
                if (state != State.Play) return;
                anim = warn ?? PickLine(); anim.t = 0; warn = null;
                shiftTotal = shiftTimer = ShiftInterval();
                shake = 4; Sfx.Play("shift");
                return;
            }
            rotTimer -= dt;
            if (rotTimer <= 0) { rotTimer = Data.RotMs; TurnCorridors(); }

            P.atkCd -= dt;
            foreach (var k in P.cd.Keys.ToList()) P.cd[k] = Mathf.Max(0, P.cd[k] - dt);
            P.shield = Mathf.Max(0, P.shield - dt);
            P.lamp = Mathf.Max(0, P.lamp - dt);
            P.lunge = Mathf.Max(0, P.lunge - dt / 160);
            P.slash = Mathf.Max(0, P.slash - dt / 220);
            P.hurt = Mathf.Max(0, P.hurt - dt / 450);
            if (P.t < 1) { P.t = Mathf.Min(1, P.t + dt / P.dur); if (P.t >= 1) OnArrive(); }
            if (state != State.Play) return;
            if (P.t >= 1)
            {
                int d = queued != 0 ? queued : held;
                if (d != 0 && TryMove(d)) queued = 0;
            }
            if (state != State.Play) return;

            // Kat başında kısa bir hazırlanma süresi: canavarlar henüz kıpırdamaz
            if (grace > 0) { grace -= dt; return; }
            var pd = Bfs(P.x, P.y);
            var opts = new List<int>(4);
            foreach (var m in monsters.ToList())
            {
                m.atkCd -= dt;
                m.hurt = Mathf.Max(0, m.hurt - dt / 250);
                m.lunge = Mathf.Max(0, m.lunge - dt / 220);
                if (m.t < 1) m.t = Mathf.Min(1, m.t + dt / m.dur);
                int dx = P.x - m.x, dy = P.y - m.y;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1 && !P.jumping)
                {
                    int d = D.DirOf(dx, dy);
                    if (m.phase || CanPass(m.x, m.y, d))
                    {
                        if (m.atkCd <= 0 && m.t >= 1)
                        {
                            m.atkCd = 1000; m.lunge = 1; m.face = d;
                            HurtPlayer(m.atk);
                            if (state != State.Play) return;
                        }
                        continue;
                    }
                }
                m.timer -= dt;
                if (m.timer > 0 || m.t < 1) continue;
                m.timer = m.move * Rnd(.85f, 1.15f);
                // Ruh Tuzağı duvarların içinden geçer ve kuş uçuşu yaklaşır
                opts.Clear();
                foreach (var d in D.ALL)
                {
                    int nx = m.x + D.DX(d), ny = m.y + D.DY(d);
                    if ((m.phase ? Inside(nx, ny) : CanPass(m.x, m.y, d)) && !Occupied(nx, ny)) opts.Add(d);
                }
                if (opts.Count == 0) continue;
                int md = m.phase ? Mathf.Abs(dx) + Mathf.Abs(dy) : pd[m.y, m.x];
                bool chasing = md > 0 && md <= m.aggro;
                int dir = 0;
                if (chasing && !(m.erratic > 0 && Random.value < m.erratic))
                {
                    int Near(int o) => m.phase ? Mathf.Abs(dx - D.DX(o)) + Mathf.Abs(dy - D.DY(o)) : pd[m.y + D.DY(o), m.x + D.DX(o)];
                    var better = opts.Where(o => Near(o) == md - 1).ToList();
                    if (better.Count > 0) dir = Pick(better);
                    else if (m.erratic <= 0) continue;
                }
                if (dir == 0)
                {
                    var fwd = opts.Where(o => o != D.Opp(m.face)).ToList();
                    dir = Pick(fwd.Count > 0 && Random.value < .75f ? fwd : opts);
                }
                m.face = dir;
                m.MoveTo(m.x + D.DX(dir), m.y + D.DY(dir), m.dur);
            }
        }

        void UpdateFx(float dt)
        {
            shake = Mathf.Max(0, shake - dt / 40);
            if (P != null) P.spinFx = Mathf.Max(0, P.spinFx - dt / 350);
            foreach (var b in bolts)
            {
                b.t += dt / 170;
                if (b.t >= 1 && !b.done)
                {
                    b.done = true;
                    Dust(b.b.x, b.b.y, Data.C("#9ad0ff"), 10);
                    if (b.target != null && monsters.Contains(b.target) && state == State.Play) HitMonster(b.target, AtkPow);
                }
            }
            bolts.RemoveAll(b => b.t >= 1.4f);
            foreach (var f in floaters) { f.life -= dt / 950; f.pos.y -= dt / 1300; }
            floaters.RemoveAll(f => f.life <= 0);
            foreach (var q in parts)
            {
                q.life -= dt / q.ttl;
                q.pos += q.vel * dt / 1000;
                if (!q.floaty) q.vel *= .94f;
            }
            parts.RemoveAll(q => q.life <= 0);
        }

        public void Floater(string text, float x, float y, Color color, bool big = false) =>
            floaters.Add(new Floater { text = text, pos = new Vector2(x, y), color = color, big = big });

        public void Dust(float x, float y, Color color, int k)
        {
            for (int i = 0; i < k; i++)
            {
                float a = Rnd(0, Mathf.PI * 2), s = Rnd(.6f, 2.4f);
                parts.Add(new Particle { pos = new Vector2(x, y), vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, color = color, ttl = Rnd(300, 650), size = Rnd(.04f, .09f) });
            }
        }

        /* ---------- Girdi ---------- */
        public void PressDir(int d)
        {
            keysDown.Remove(d); keysDown.Add(d);
            queued = d; held = d;
        }
        public void ReleaseDir(int d)
        {
            keysDown.Remove(d);
            held = keysDown.Count > 0 ? keysDown[keysDown.Count - 1] : 0;
        }
        // Joystick: basılı tutulan yönü doğrudan belirler
        public void SetStick(int d)
        {
            if (d != 0 && d != held) queued = d;
            held = d;
            if (d == 0) keysDown.Clear();
        }
        public bool Moving => P != null && (P.t < 1 || (held != 0 && state == State.Play));
    }
}
