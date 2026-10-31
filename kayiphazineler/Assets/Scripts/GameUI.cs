using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace KayipHazineler
{
    // Sahne ne olursa olsun oyun kendini kurar: boş bir sahnede bile Play'e basmak yeter
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Game>() == null) new GameObject("KayipHazineler").AddComponent<Game>();
        }
    }

    // Yaşam döngüsü, girdi ve arayüz (HUD, menüler, mağaza)
    public partial class Game
    {
        string toastText;
        float toastUntil;
        string shopTab = "weapon";
        int selIndex;
        string nameField = "";
        State scoresFrom = State.Title;
        int stickDir, stickTouch = -1;
        bool stickMouse;
        Vector2 stickVec;

        public float UiScale => Mathf.Max(.5f, Mathf.Min(Screen.width / 420f, Screen.height / 760f));
        float VW => Screen.width / UiScale;
        float VH => Screen.height / UiScale;
        const float HudH = 92, CtrlH = 150;
        float HudHeightPx() => HudH * UiScale;
        float ControlsHeightPx() => CtrlH * UiScale;

        public void Toast(string msg) { toastText = msg; toastUntil = Time.unscaledTime + 2.4f; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            SetupView();
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            Sfx.Init(gameObject);
            ShowTitle();
        }

        void ShowTitle()
        {
            // Menünün arkasında örnek bir labirent dursun
            R = RunData.Fresh(Data.ClassIds[Random.Range(0, Data.ClassIds.Length)]);
            StartLevel(true);
            state = State.Title;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f) * 1000;
            HandleKeys();
            HandleTouches();
            Tick(dt);
            Render();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && state == State.Play) { Save.SaveRun(R); state = State.Pause; }
        }

        void OnApplicationQuit()
        {
            if (state == State.Play || state == State.Pause) Save.SaveRun(R);
        }

        /* ---------- Girdi ---------- */
        static readonly (Key key, int dir)[] DirKeys =
        {
            (Key.UpArrow, D.N), (Key.W, D.N), (Key.RightArrow, D.E), (Key.D, D.E),
            (Key.DownArrow, D.S), (Key.S, D.S), (Key.LeftArrow, D.W), (Key.A, D.W),
        };

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (state == State.Select) return; // isim kutusuna yazılıyor olabilir
            foreach (var (k, d) in DirKeys)
            {
                if (kb[k].wasPressedThisFrame) PressDir(d);
                if (kb[k].wasReleasedThisFrame) ReleaseDir(d);
            }
            if (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) Attack();
            if (kb.qKey.wasPressedThisFrame) UseSkill("spin");
            if (kb.eKey.wasPressedThisFrame) UseSkill("jump");
            if (kb.rKey.wasPressedThisFrame) UseSkill("shield");
            if (kb.fKey.wasPressedThisFrame) DrinkPotion();
            if (kb.lKey.wasPressedThisFrame) UseLamp();
            if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) TogglePause();
        }

        void TogglePause()
        {
            if (state == State.Pause) state = State.Play;
            else if (state == State.Play) state = State.Pause;
        }

        // Oyun içi dokunmatik kontroller: joystick ve yuvarlak düğmeler (aynı anda çoklu dokunuş)
        struct Ctrl { public string id; public Vector2 c; public float r; }

        List<Ctrl> Controls()
        {
            float vw = VW, vh = VH;
            return new List<Ctrl>
            {
                new Ctrl { id = "attack", c = new Vector2(vw - 70, vh - 80), r = 46 },
                new Ctrl { id = "potion", c = new Vector2(vw - 250, vh - 112), r = 25 },
                new Ctrl { id = "jump",   c = new Vector2(vw - 195, vh - 112), r = 25 },
                new Ctrl { id = "shield", c = new Vector2(vw - 140, vh - 120), r = 25 },
                new Ctrl { id = "spin",   c = new Vector2(vw - 222, vh - 50),  r = 25 },
                new Ctrl { id = "lamp",   c = new Vector2(vw - 167, vh - 50),  r = 25 },
                new Ctrl { id = "pause",  c = new Vector2(vw - 34, 34),        r = 22 },
            };
        }
        Vector2 StickCenter => new Vector2(80, VH - 76);
        const float StickR = 60;

        Vector2 ToGui(Vector2 screen) => new Vector2(screen.x / UiScale, (Screen.height - screen.y) / UiScale);

        void HandleTouches()
        {
            bool play = state == State.Play;
            Vector2? stickPos = null;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    int id = t.touchId.ReadValue();
                    var p = ToGui(t.position.ReadValue());
                    if (t.press.wasPressedThisFrame) OnPointerDown(p, id, false);
                    if (t.press.isPressed && id == stickTouch) stickPos = p;
                    if (!t.press.isPressed && id == stickTouch) stickTouch = -1;
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var p = ToGui(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame && (ts == null || !ts.press.isPressed)) OnPointerDown(p, -1, true);
                if (mouse.leftButton.isPressed && stickMouse) stickPos = p;
                if (!mouse.leftButton.isPressed) stickMouse = false;
            }
            int dir = 0;
            stickVec = Vector2.zero;
            if (play && stickPos.HasValue)
            {
                var v = stickPos.Value - StickCenter;
                stickVec = Vector2.ClampMagnitude(v, StickR - 20);
                if (v.magnitude > 14) dir = Mathf.Abs(v.x) > Mathf.Abs(v.y) ? (v.x > 0 ? D.E : D.W) : (v.y > 0 ? D.S : D.N);
            }
            // Joystick yalnızca değiştiğinde bildirir; klavyeyle çakışmasın
            if (dir != stickDir) { stickDir = dir; SetStick(dir); }
        }

        void OnPointerDown(Vector2 p, int touchId, bool isMouse)
        {
            if (state != State.Play) return;
            if ((p - StickCenter).magnitude <= StickR + 20)
            {
                if (isMouse) stickMouse = true; else stickTouch = touchId;
                return;
            }
            foreach (var c in Controls())
                if ((p - c.c).magnitude <= c.r + 6) { Press(c.id); return; }
        }

        void Press(string id)
        {
            switch (id)
            {
                case "attack": Attack(); break;
                case "potion": DrinkPotion(); break;
                case "lamp": UseLamp(); break;
                case "pause": TogglePause(); Sfx.Play("click"); break;
                default: UseSkill(id); break;
            }
        }

        /* ---------- Stiller ---------- */
        GUIStyle sCard, sTitle, sH2, sBody, sSmall, sBtn, sGhost, sTab, sTabOn, sCenter, sOutline, sField, sRowBox;
        Texture2D texWhite, texCircle;
        bool stylesReady;

        static readonly Color Gold = Data.C("#f5c04a"), Ink = Data.C("#e8eefb"), Muted = Data.C("#93a2c0"), Hp = Data.C("#e0413f"),
            Panel = Data.C("#111a2e"), Line = Data.C("#2a3a5c"), Xp = Data.C("#4a8cff");

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            texWhite = Texture2D.whiteTexture;
            texCircle = PixelArt.Circle.texture;
            GUIStyle L(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft) => new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true,
                normal = { textColor = c }
            };
            sCard = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 16, 16), normal = { background = PixelArt.Solid(new Color(.07f, .1f, .18f, .97f)) } };
            sRowBox = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 3, 3), normal = { background = PixelArt.Solid(Data.C("#0f1729")) } };
            sTitle = L(30, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            sH2 = L(22, Ink, FontStyle.Bold);
            sBody = L(15, Data.C("#c8d2e6"));
            sSmall = L(12, Muted);
            sCenter = L(15, Data.C("#c8d2e6"), FontStyle.Normal, TextAnchor.MiddleCenter);
            sOutline = L(16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            sOutline.wordWrap = false;
            GUIStyle B(Color bg, Color hover, Color fg, int size) => new GUIStyle(GUI.skin.button)
            {
                fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fixedHeight = 0,
                margin = new RectOffset(0, 0, 5, 5), padding = new RectOffset(10, 10, 8, 8),
                normal = { background = PixelArt.Solid(bg), textColor = fg },
                hover = { background = PixelArt.Solid(hover), textColor = fg },
                active = { background = PixelArt.Solid(hover * .9f), textColor = fg },
            };
            sBtn = B(Gold, Data.C("#ffd46a"), Data.C("#1a1206"), 18);
            sBtn.fixedHeight = 48;
            sGhost = B(Data.C("#1a2540"), Data.C("#24345a"), Ink, 16);
            sGhost.fixedHeight = 42;
            sTab = B(Data.C("#141e34"), Data.C("#1d2a48"), Muted, 13);
            sTabOn = B(Data.C("#3a2d10"), Data.C("#4a3a16"), Gold, 13);
            sField = new GUIStyle(GUI.skin.textField) { fontSize = 17, padding = new RectOffset(10, 10, 8, 8), normal = { background = PixelArt.Solid(Data.C("#0c1322")), textColor = Ink },
                focused = { background = PixelArt.Solid(Data.C("#0f1a30")), textColor = Ink } };
        }

        void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, texWhite); GUI.color = o; }
        void Disc(Vector2 c, float r, Color col) { var o = GUI.color; GUI.color = col; GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2, r * 2), texCircle); GUI.color = o; }
        void Icon(Rect r, Sprite s, float alpha = 1) { if (s == null) return; var o = GUI.color; GUI.color = new Color(1, 1, 1, alpha); GUI.DrawTexture(r, s.texture, ScaleMode.ScaleToFit); GUI.color = o; }
        void Outlined(Rect r, string text, Color c, int size)
        {
            sOutline.fontSize = size;
            sOutline.normal.textColor = new Color(0, 0, 0, c.a * .85f);
            foreach (var o in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(1, 1) })
                GUI.Label(new Rect(r.x + o.x * 1.5f, r.y + o.y * 1.5f, r.width, r.height), text, sOutline);
            sOutline.normal.textColor = c;
            GUI.Label(r, text, sOutline);
        }
        void Bar(Rect r, float k, Color bg, Color fg) { Fill(r, bg); Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(k), r.height), fg); }

        /* ---------- Çizim ---------- */
        void OnGUI()
        {
            InitStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1));
            if (state == State.Play || state == State.Pause || state == State.Shop || state == State.Over)
            {
                DrawFloaters();
                DrawHud();
                DrawControls();
            }
            DrawToast();
            if (state != State.Play) Fill(new Rect(0, 0, VW, VH), new Color(.02f, .03f, .07f, state == State.Title || state == State.Select || state == State.Scores ? .55f : .65f));
            switch (state)
            {
                case State.Title: TitleScreen(); break;
                case State.Select: SelectScreen(); break;
                case State.Pause: PauseScreen(); break;
                case State.Shop: ShopScreen(); break;
                case State.Over: OverScreen(); break;
                case State.Scores: ScoresScreen(); break;
            }
        }

        void DrawFloaters()
        {
            if (cam == null) return;
            foreach (var f in floaters)
            {
                var sp = cam.WorldToScreenPoint(new Vector3(f.pos.x, -f.pos.y, 0));
                var g = ToGui(sp);
                var c = f.color; c.a = Mathf.Clamp01(f.life * 1.6f);
                Outlined(new Rect(g.x - 60, g.y - 14, 120, 28), f.text, c, f.big ? 20 : 15);
            }
        }

        void DrawHud()
        {
            float vw = VW;
            var top = new Rect(8, 8, vw - 16, 54);
            Fill(top, new Color(.05f, .08f, .14f, .95f));
            Fill(new Rect(top.x, top.yMax - 1, top.width, 1), Line);
            // Portre
            Disc(new Vector2(38, 35), 24, Data.C("#1a2540"));
            var hs = PixelArt.Heroes.TryGetValue(R.hero, out var set) ? set : PixelArt.Heroes["knight"];
            Icon(new Rect(16, 12, 44, 44), hs.front[0].normal);
            float bw = Mathf.Min(220, vw * .36f);
            var hpR = new Rect(70, 15, bw, 16);
            Bar(hpR, (float)R.hp / MaxHp, Data.C("#2a1420"), Raging ? Data.C("#ff6a2a") : Hp);
            Outlined(hpR, $"{R.hp}/{MaxHp}", Color.white, 12);
            GUI.Label(new Rect(70, 33, 50, 18), $"Lv. {R.lvl}", sSmall);
            Bar(new Rect(116, 40, bw - 46, 6), (float)R.xp / Data.XpNeed(R.lvl), Data.C("#142342"), Xp);
            // Sağ: anahtar, altın, puan
            float rx = vw - 64;
            var scoreR = new Rect(rx - 120, 36, 116, 20);
            sSmall.alignment = TextAnchor.MiddleRight;
            GUI.Label(scoreR, $"Puan: <b><color=#e8eefb>{R.score}</color></b>", sSmall);
            sSmall.alignment = TextAnchor.UpperLeft;
            Icon(new Rect(rx - 60, 13, 18, 18), PixelArt.Get("coin").normal);
            GUI.Label(new Rect(rx - 40, 12, 40, 20), $"<b><color=#f5c04a>{R.gold}</color></b>", sBody);
            Icon(new Rect(rx - 112, 13, 20, 20), PixelArt.Get("key").normal, hasKey ? 1 : .3f);
            GUI.Label(new Rect(rx - 90, 12, 24, 20), hasKey ? "<b>1</b>" : "0", sBody);
            // Mola düğmesi
            Disc(new Vector2(vw - 34, 34), 20, Data.C("#1a2540"));
            Fill(new Rect(vw - 40, 26, 4, 16), Ink); Fill(new Rect(vw - 32, 26, 4, 16), Ink);

            // Kat çubuğu
            var fb = new Rect(8, 66, vw - 16, 22);
            GUI.Label(new Rect(fb.x + 4, fb.y + 2, fb.width - 160, 20), $"<color=#f5c04a><b>Kat {R.floor}</b></color> · {Theme.name} · Canavar {monsters.Count}", sSmall);
            float sec = Mathf.Max(0, shiftTimer / 1000f);
            sSmall.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(fb.xMax - 170, fb.y + 2, 96, 20), warn != null ? "<color=#4aa8ff><b>Kayıyor!</b></color>" : $"Kayma {sec:0.0} sn", sSmall);
            sSmall.alignment = TextAnchor.UpperLeft;
            Bar(new Rect(fb.xMax - 68, fb.y + 9, 64, 5), 1 - shiftTimer / Mathf.Max(1, shiftTotal), Data.C("#142342"), warn != null ? Data.C("#4aa8ff") : Data.C("#3a6ab8"));
        }

        void DrawControls()
        {
            bool touch = Touchscreen.current != null || Application.isMobilePlatform;
            // Joystick
            var sc = StickCenter;
            Disc(sc, StickR, new Color(.1f, .15f, .25f, .7f));
            Disc(sc, StickR - 14, new Color(.06f, .09f, .16f, .7f));
            Disc(sc + stickVec, 26, new Color(.6f, .68f, .85f, .85f));
            if (!touch) GUI.Label(new Rect(sc.x - 60, sc.y + StickR - 4, 120, 18), "WASD / Oklar", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter });

            foreach (var c in Controls())
            {
                if (c.id == "pause") continue;
                bool locked = (c.id == "jump" || c.id == "shield" || c.id == "spin") && !SkillOpen(c.id);
                float cdLeft = 0, cdMax = 1;
                if (P != null && P.cd.ContainsKey(c.id)) { cdLeft = P.cd[c.id]; cdMax = c.id == "lamp" ? Data.LampCd : c.id == "potion" ? 800 : SkillCd(c.id); }
                Disc(c.c, c.r + 2, c.id == "attack" ? Data.C("#d8e2f5") : Line);
                Disc(c.c, c.r, c.id == "attack" ? Data.C("#2a3a60") : Data.C("#16213a"));
                string label = "", hint = "";
                switch (c.id)
                {
                    case "attack": label = R.hero == "mage" ? "Büyü" : "Saldır"; hint = "Boşluk"; break;
                    case "potion": Icon(new Rect(c.c.x - 14, c.c.y - 16, 28, 28), PixelArt.Get("potion").normal); hint = "F"; break;
                    case "jump": label = "Zıpla"; hint = "E"; break;
                    case "shield": label = "Kalkan"; hint = "R"; break;
                    case "spin": label = "Döner"; hint = "Q"; break;
                    case "lamp": label = "Fener"; hint = "L"; break;
                }
                if (label != "") Outlined(new Rect(c.c.x - 40, c.c.y - 12, 80, 24), label, locked ? Muted : Ink, c.id == "attack" ? 17 : 11);
                // Bekleme süresi: düğme aşağıdan yukarı kararır
                if (cdLeft > 0)
                {
                    float k = Mathf.Clamp01(cdLeft / cdMax), hgt = c.r * 2 * k;
                    GUI.BeginGroup(new Rect(c.c.x - c.r, c.c.y + c.r - hgt, c.r * 2, hgt));
                    var o = GUI.color; GUI.color = new Color(0, 0, 0, .6f);
                    GUI.DrawTexture(new Rect(0, hgt - c.r * 2, c.r * 2, c.r * 2), texCircle);
                    GUI.color = o;
                    GUI.EndGroup();
                }
                if (locked)
                {
                    var sk = Data.SkillById(c.id);
                    Fill(new Rect(c.c.x + c.r - 22, c.c.y + c.r - 14, 28, 16), Gold);
                    GUI.Label(new Rect(c.c.x + c.r - 22, c.c.y + c.r - 15, 28, 16), $"<color=#1a1206><b>Lv{sk.lvl}</b></color>", new GUIStyle(sSmall) { fontSize = 11, alignment = TextAnchor.MiddleCenter });
                }
                if (c.id == "potion")
                {
                    Disc(new Vector2(c.c.x + c.r - 4, c.c.y + c.r - 6), 9, Gold);
                    GUI.Label(new Rect(c.c.x + c.r - 13, c.c.y + c.r - 15, 18, 18), $"<color=#1a1206><b>{R.potions}</b></color>", new GUIStyle(sSmall) { fontSize = 11, alignment = TextAnchor.MiddleCenter });
                }
                if (!touch && hint != "") GUI.Label(new Rect(c.c.x - 30, c.c.y - c.r - 15, 60, 16), hint, new GUIStyle(sSmall) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(toastText) || Time.unscaledTime > toastUntil) return;
            float a = Mathf.Clamp01((toastUntil - Time.unscaledTime) * 3);
            var size = sCenter.CalcSize(new GUIContent(toastText));
            float tw = Mathf.Min(VW - 32, size.x + 32);
            var r = new Rect((VW - tw) / 2, HudH + 6, tw, 34);
            Fill(r, new Color(.12f, .09f, .03f, .92f * a));
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(Gold.r, Gold.g, Gold.b, a));
            var c = GUI.color; GUI.color = new Color(1, 1, 1, a);
            GUI.Label(r, $"<color=#ffe9b0>{toastText}</color>", sCenter);
            GUI.color = c;
        }

        // Ortalanmış kart: içerik GUILayout ile akar
        void Card(float width, Action body)
        {
            float cw = Mathf.Min(VW - 24, width);
            GUILayout.BeginArea(new Rect((VW - cw) / 2, 12, cw, VH - 24));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(sCard);
            body();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        bool Btn(string text, bool ghost = false, bool enabled = true)
        {
            var o = GUI.enabled; GUI.enabled = enabled;
            bool r = GUILayout.Button(text, ghost ? sGhost : sBtn);
            GUI.enabled = o;
            if (r) Sfx.Play("click");
            return r;
        }

        void KV(params (string k, string v)[] pairs)
        {
            GUILayout.BeginHorizontal();
            foreach (var (k, v) in pairs)
            {
                GUILayout.BeginVertical(sRowBox);
                GUILayout.Label(k, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
                GUILayout.Label($"<b>{v}</b>", new GUIStyle(sBody) { alignment = TextAnchor.MiddleCenter, fontSize = 17 });
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        /* ---------- Ekranlar ---------- */
        void TitleScreen()
        {
            Card(420, () =>
            {
                GUILayout.Label("Labirent", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 14 });
                GUILayout.Label("Kayıp Hazineler", sTitle);
                GUILayout.Space(6);
                GUILayout.Label("Kayan koridorlarda anahtarı bul, canavarları alt et, çıkışa ulaş. Her 5. katta Minotor seni bekler!", sCenter);
                GUILayout.Space(10);
                // Kahraman dizisi
                var r = GUILayoutUtility.GetRect(10, 80);
                float sx = r.center.x - 2 * 70 + 35;
                for (int i = 0; i < Data.ClassIds.Length; i++)
                {
                    var hs = PixelArt.Heroes[Data.ClassIds[i]];
                    int frame = (int)(Time.time / .3f + i) % 2 == 0 ? 0 : 1;
                    Icon(new Rect(sx + i * 70 - 34, r.y, 68, 68), hs.front[frame].normal);
                }
                var saved = Save.LoadRun();
                if (saved != null && !Data.Classes.ContainsKey(saved.hero)) saved = null;
                if (saved != null && Btn($"Devam et · {Data.Classes[saved.hero].name} · Kat {saved.floor}")) NewRun(saved);
                if (Btn(saved != null ? "Yeni oyun" : "Oyuna başla", saved != null)) { state = State.Select; selIndex = 0; nameField = ""; }
                GUILayout.BeginHorizontal();
                if (Btn("Rekor tablosu", true)) { scoresFrom = State.Title; state = State.Scores; }
                if (Btn(Sfx.On ? "Ses: Açık" : "Ses: Kapalı", true)) { Sfx.On = !Sfx.On; Save.AudioOn = Sfx.On; }
                GUILayout.EndHorizontal();
                var best = Save.LoadScores().FirstOrDefault();
                if (best != null) GUILayout.Label($"En iyi: <color=#f5c04a><b>{best.score}</b></color> · {best.name}", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
                GUILayout.Label("Klavye: WASD/oklar · Boşluk saldırı · Q/E/R yetenek · F iksir · L fener · Esc mola", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 11 });
            });
        }

        void SelectScreen()
        {
            Card(460, () =>
            {
                GUILayout.Label("Kahramanını seç", sH2);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Data.ClassIds.Length; i++)
                    if (GUILayout.Button(Data.Classes[Data.ClassIds[i]].name, i == selIndex ? sTabOn : sTab)) { selIndex = i; Sfx.Play("click"); }
                GUILayout.EndHorizontal();
                var id = Data.ClassIds[selIndex];
                var C = Data.Classes[id];
                GUILayout.BeginHorizontal(sRowBox);
                var r = GUILayoutUtility.GetRect(110, 110, GUILayout.Width(110));
                Disc(r.center, 52, Data.C("#2f2716"));
                var hs = PixelArt.Heroes[id];
                int frame = new[] { 0, 1, 0, 2 }[(int)(Time.time / .15f) % 4];
                Icon(new Rect(r.x + 5, r.y + 2, 100, 100), hs.front[frame].normal);
                GUILayout.BeginVertical();
                GUILayout.Label($"<b>{C.name}</b> · <color=#93a2c0>{C.role}</color>", sBody);
                StatBar("Can", C.hp, 140);
                StatBar("Saldırı", C.atk, 14);
                StatBar("Hız", C.speed, 125);
                StatBar("Kritik", C.crit, 15);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Label($"<color=#f5c04a>Yetenek:</color> {C.perk}", sBody);
                GUILayout.Label($"Silah: {Data.WeaponNames[C.weapon][0]}", sSmall);
                GUILayout.Space(6);
                GUILayout.Label("Kahramanının adı", sSmall);
                GUILayout.BeginHorizontal();
                nameField = GUILayout.TextField(nameField, 14, sField, GUILayout.Height(40));
                if (GUILayout.Button("Rastgele", sGhost, GUILayout.Width(100))) { nameField = Data.Names[Random.Range(0, Data.Names.Length)]; GUI.FocusControl(null); Sfx.Play("click"); }
                GUILayout.EndHorizontal();
                if (Btn("Labirente gir"))
                {
                    GUI.FocusControl(null);
                    var run = RunData.Fresh(id);
                    run.name = nameField.Trim();
                    Save.ClearRun();
                    NewRun(run);
                }
                if (Btn("Geri", true)) { GUI.FocusControl(null); state = State.Title; }
            });
        }

        void StatBar(string label, int v, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sSmall, GUILayout.Width(56));
            var r = GUILayoutUtility.GetRect(100, 10, GUILayout.ExpandWidth(true));
            Bar(new Rect(r.x, r.y + 5, r.width, 8), (float)v / max, Data.C("#142342"), Gold);
            GUILayout.Label(v.ToString(), sSmall, GUILayout.Width(30));
            GUILayout.EndHorizontal();
        }

        void CharacterInfo()
        {
            GUILayout.BeginHorizontal(sRowBox);
            var r = GUILayoutUtility.GetRect(72, 72, GUILayout.Width(72));
            Icon(r, PixelArt.Heroes[R.hero].front[0].normal);
            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{HeroName}</b> · <color=#93a2c0>{Cls.name} · Lv. {R.lvl}</color>", sBody);
            var xr = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
            Bar(new Rect(xr.x, xr.y + 3, xr.width, 8), (float)R.xp / Data.XpNeed(R.lvl), Data.C("#142342"), Xp);
            GUILayout.Label($"XP {R.xp}/{Data.XpNeed(R.lvl)} · Kat {R.floor} · Av {R.kills}", sSmall);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            KV(("HP", $"{R.hp}/{MaxHp}"), ("Saldırı", AtkPow.ToString()), ("Savunma", DefPow.ToString()), ("Kritik", $"%{CritPct}"));
            GUILayout.Label($"Silah: <b>{WeaponName(R.weapon)}</b> · Zırh: <b>{Data.Armors[R.armor].name}</b>", sSmall);
            var accs = R.acc.Select(a => Array.Find(Data.Accs, x => x.id == a)?.name).Where(n => n != null).ToList();
            GUILayout.Label("Aksesuar: " + (accs.Count > 0 ? string.Join(", ", accs) : "yok"), sSmall);
            GUILayout.BeginHorizontal();
            foreach (var s in Data.Skills)
            {
                bool open = SkillOpen(s.id);
                GUILayout.BeginVertical(sRowBox);
                GUILayout.Label($"<b>{s.name}</b>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = open ? Ink : Muted } });
                GUILayout.Label(open ? $"{SkillCd(s.id) / 1000f:0.#} sn" : $"Lv{s.lvl}'de açılır", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 11 });
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        void PauseScreen()
        {
            Card(440, () =>
            {
                GUILayout.Label("Mola", sH2);
                CharacterInfo();
                if (Btn("Devam et")) state = State.Play;
                GUILayout.BeginHorizontal();
                if (Btn(Sfx.On ? "Ses: Açık" : "Ses: Kapalı", true)) { Sfx.On = !Sfx.On; Save.AudioOn = Sfx.On; }
                if (Btn("Ana menü", true)) { Save.SaveRun(R); ShowTitle(); }
                GUILayout.EndHorizontal();
            });
        }

        void ShopScreen()
        {
            Card(460, () =>
            {
                GUILayout.Label($"Kat {R.floor} temizlendi · +{lastBonus} puan · canının %20'si yenilendi", sSmall);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mağaza", sH2);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"<color=#f5c04a><b>{R.gold} altın</b></color>", new GUIStyle(sBody) { fontSize = 18, alignment = TextAnchor.MiddleRight });
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var (id, name) in new[] { ("weapon", "Silah"), ("armor", "Zırh"), ("acc", "Aksesuar"), ("potion", "İksir") })
                    if (GUILayout.Button(name, shopTab == id ? sTabOn : sTab)) { shopTab = id; Sfx.Play("click"); }
                GUILayout.EndHorizontal();

                if (shopTab == "weapon")
                    for (int i = 1; i < Data.Weapons.Length; i++)
                    {
                        var wpn = Data.Weapons[i];
                        ShopRow(WeaponName(i), $"+{wpn.stat} Saldırı Gücü", wpn.cost, Cls.weapon == "staff" ? Data.StaffOrbs[i] : wpn.color,
                            R.weapon == i ? "eq" : R.weapon > i ? "own" : R.gold >= wpn.cost ? "buy" : "no", "weapon:" + i);
                    }
                else if (shopTab == "armor")
                    for (int i = 1; i < Data.Armors.Length; i++)
                    {
                        var a = Data.Armors[i];
                        ShopRow(a.name, $"+{a.stat} Savunma", a.cost, a.color, R.armor == i ? "eq" : R.armor > i ? "own" : R.gold >= a.cost ? "buy" : "no", "armor:" + i);
                    }
                else if (shopTab == "acc")
                    foreach (var a in Data.Accs)
                        ShopRow(a.name, a.desc, a.cost, Gold, R.Has(a.id) ? "eq" : R.gold >= a.cost ? "buy" : "no", "acc:" + a.id);
                else
                    ShopRow(Data.PotionName, $"+{Data.PotionHeal} HP · çantada {R.potions}/{Data.PotionMax}", Data.PotionCost, Hp,
                        R.potions >= Data.PotionMax ? "full" : R.gold >= Data.PotionCost ? "buy" : "no", "potion");

                KV(("HP", $"{R.hp}/{MaxHp}"), ("Saldırı", AtkPow.ToString()), ("Savunma", DefPow.ToString()), ("Kritik", $"%{CritPct}"));
                if (Btn($"Kat {R.floor + 1}'e in")) NextFloor();
            });
        }

        void ShopRow(string name, string desc, int cost, Color color, string st, string what)
        {
            GUILayout.BeginHorizontal(sRowBox);
            var r = GUILayoutUtility.GetRect(14, 38, GUILayout.Width(14));
            Fill(new Rect(r.x, r.y + 4, 8, 30), color);
            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{name}</b>", sBody);
            GUILayout.Label(desc, sSmall);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            bool owned = st == "own" || st == "eq";
            if (!owned) GUILayout.Label($"<color=#f5c04a><b>{cost}</b></color>", new GUIStyle(sBody) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(50), GUILayout.Height(38));
            string label = st == "eq" ? "Kuşanıldı" : st == "own" ? "Sahipsin" : st == "full" ? "Dolu" : "Satın al";
            var o = GUI.enabled; GUI.enabled = st == "buy";
            if (GUILayout.Button(label, st == "buy" ? sBtn : sGhost, GUILayout.Width(100), GUILayout.Height(38))) Buy(what);
            GUI.enabled = o;
            GUILayout.EndHorizontal();
        }

        void OverScreen()
        {
            Card(420, () =>
            {
                GUILayout.Label("<color=#e0413f>Labirent seni yuttu</color>", sH2);
                GUILayout.Label($"{HeroName}, Kat {R.floor}'de düştü. {R.kills} canavar avladı, Lv. {R.lvl}'e ulaştı.", sBody);
                var best = Save.LoadScores().FirstOrDefault();
                KV(("Puan", R.score.ToString()), ("En iyi", (best?.score ?? 0).ToString()), ("Kat", R.floor.ToString()), ("Av", R.kills.ToString()));
                if (lastRank == 0) GUILayout.Label("<color=#f5c04a><b>Yeni rekor! Tablonun zirvesindesin.</b></color>", sCenter);
                else if (lastRank > 0) GUILayout.Label($"<color=#f5c04a><b>Rekor tablosunda {lastRank + 1}. sıraya girdin!</b></color>", sCenter);
                if (Btn("Yeniden başla")) { state = State.Select; selIndex = Array.IndexOf(Data.ClassIds, R.hero); nameField = R.name; }
                GUILayout.BeginHorizontal();
                if (Btn("Rekor tablosu", true)) { scoresFrom = State.Over; state = State.Scores; }
                if (Btn("Ana menü", true)) ShowTitle();
                GUILayout.EndHorizontal();
            });
        }

        void ScoresScreen()
        {
            Card(440, () =>
            {
                GUILayout.Label("Rekor tablosu", sH2);
                var list = Save.LoadScores();
                if (list.Count == 0) GUILayout.Label("Henüz rekor yok. İlk sen ol!", sCenter);
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    GUILayout.BeginHorizontal(sRowBox);
                    GUILayout.Label($"<b>{i + 1}</b>", new GUIStyle(sBody) { normal = { textColor = i == 0 ? Gold : Ink } }, GUILayout.Width(26));
                    var r = GUILayoutUtility.GetRect(30, 30, GUILayout.Width(30));
                    if (PixelArt.Heroes.TryGetValue(e.hero ?? "knight", out var hs)) Icon(r, hs.front[0].normal);
                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{e.name}</b>", sBody);
                    GUILayout.Label($"Kat {e.floor} · Lv. {e.lvl} · {e.kills} av", sSmall);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"<color=#f5c04a><b>{e.score}</b></color>", new GUIStyle(sBody) { alignment = TextAnchor.MiddleRight, fontSize = 18 }, GUILayout.Height(36));
                    GUILayout.EndHorizontal();
                }
                if (Btn("Geri", true)) state = scoresFrom;
            });
        }
    }
}
