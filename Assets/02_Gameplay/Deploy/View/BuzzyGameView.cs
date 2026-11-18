using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M2: dựng màn chơi từ BuzzyLevel + BuzzyLayoutConfig + BuzzyAssets, nối luật lõi (BusGame) với chuyển động.
    /// Chuyển động chạy bằng Tick(dt) thuần (Update gọi Tick) để bộ chụp trong Editor bước thời gian xác định.
    /// </summary>
    public partial class BuzzyGameView : MonoBehaviour
    {
        // ----- cấu hình + trạng thái -----
        BuzzyLayoutConfig _cfg; BuzzyAssets _a; BuzzyLevel _lv; Camera _cam; Vector2 _canvasSize;
        public BusGame Game { get; private set; }
        public float Time { get; private set; }
        public float TimeScale = 1f;
        public bool Won { get; private set; }
        public float WonAt { get; private set; }
        public int TapCount { get; private set; }
        public Action<string> Log;
        public Action Vibrate;

        float _cellX0;          // x thế giới của cột 0
        float _boardZ0;         // z thế giới của hàng 0 (tâm bảng cố định, V33)
        class Pass { public Transform tr; public PixelData d; public Vector3 home; public float appearAt; public bool flying; public bool hidden; public MeshRenderer mr; public bool loose; public float phase; }
        class Flight { public Pass p; public CarV car; public float start, dur; public Vector3 from; public float phase, side; }
        class Move { public Vector3[] pts; public float[] cum; public float dur, t; public Quaternion endRot; public bool useEndRot; public float delay; public Action done; public bool keepRot; }
        class CarV
        {
            public Car car; public Transform tr; public TextMesh[] texts; public Move move; public bool leaving; public int col; public Vector3 slotPos; public GameObject lockObj; public MeshRenderer mr;
        }
        class Pop { public Transform tr; public float at, dur; public Vector3 scale; }

        readonly Dictionary<int, Pass> _pass = new Dictionary<int, Pass>();
        readonly List<Flight> _flights = new List<Flight>();
        readonly List<CarV> _cars = new List<CarV>();
        readonly List<CarV>[] _queue = { new List<CarV>(), new List<CarV>(), new List<CarV>() };
        readonly CarV[] _slotCar = new CarV[3];
        readonly List<Pop> _pops = new List<Pop>();
        Material _vehMat0; Transform _root; Canvas _canvas; RectTransform[] _fullBubbles = new RectTransform[3];
        float _inputOpenAt;
        readonly GameObject[] _pMark = new GameObject[3]; float _baseOrtho; RectTransform _uiRoot; Image _haze; System.Random _rnd = new System.Random(3);

        public Vector3 SlotPos(int i) { return new Vector3(_cfg.slotX[i], 0f, _cfg.slotZ); }
        Vector3 QueuePos(int col, int k) { return new Vector3(_cfg.queueX[col], 0f, _cfg.queueZ + (k == 0 ? 0f : 6f + (k - 1) * 4.5f)); }
        Vector3 PixelPos(PixelData d) { return new Vector3(_cellX0 + d.X * _cfg.cellPitchX, 0f, _boardZ0 + d.Y * _cfg.cellPitch); }
        Quaternion SlotRot { get { return Quaternion.Euler(0f, _cfg.slotYaw, 0f); } }

        public void Init(BuzzyLevel lv, BuzzyLayoutConfig cfg, BuzzyAssets assets, Camera cam, Vector2Int renderSize)
        {
            _lv = lv; _cfg = cfg; _a = assets; _cam = cam; _canvasSize = new Vector2(renderSize.x, renderSize.y);
            _root = new GameObject("BuzzyWorld").transform; _root.SetParent(transform, false);
            _cellX0 = cfg.boardCenterX - (lv.width * 0.5f) * cfg.cellPitchX; _boardZ0 = cfg.BoardZ0(lv.height);
            BuildEnvironment();
            // luật lõi: tọa độ ô bến theo lưới để tính "gần xe nhất"
            var slotGrid = new double[3][];
            for (int i = 0; i < 3; i++) slotGrid[i] = new double[] { (cfg.slotX[i] - _cellX0) / cfg.cellPitchX, (cfg.slotZ - _boardZ0) / cfg.cellPitch };
            Game = new BusGame(lv.ToCore(), slotGrid);
            SubscribeMechanics();
            Game.PixelArrived += OnPixelArrived;
            Game.CarEntered += OnCarEntered;
            Game.CarDeparted += OnCarDeparted;
            Game.BarrierPlaced += OnBarrier;
            BuildBoard();
            BuildMechanics();
            BuildCars();
            BuildUi();
            _inputOpenAt = cfg.inputDelay;
            InitBoosters();
            StartIntro();
        }

        // ---------------------------------------------------------------- dựng cảnh
        Material Mat(string shader, Color c) { var m = new Material(Shader.Find(shader)); m.color = c; return m; }

        GameObject Quad(string name, Color c, float cx, float z0, float z1, float halfW, float y)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(_root, false);
            q.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            q.transform.localPosition = new Vector3(cx, y, (z0 + z1) * 0.5f);
            q.transform.localScale = new Vector3(halfW * 2f, z1 - z0, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", c);
            return q;
        }

        void BuildEnvironment()
        {
            // nền chuyển sắc
            var tex = new Texture2D(1, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) tex.SetPixel(0, 63 - i, Color.Lerp(_cfg.backgroundTop, _cfg.backgroundBottom, i / 63f));
            tex.Apply();
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(bg.GetComponent<Collider>());
            bg.name = "BackgroundGradient"; bg.transform.SetParent(_cam.transform, false);
            var m = new Material(Shader.Find("Unlit/Texture")); m.mainTexture = tex; bg.GetComponent<MeshRenderer>().sharedMaterial = m;
            float dist = 150f; var bs = _cfg.BackdropSize(_cam, dist);
            bg.transform.localPosition = new Vector3(0, 0, dist); bg.transform.localScale = new Vector3(bs.x, bs.y, 1f);
            // đường + vạch đứt
            Quad("Road", _cfg.roadColor, 0f, _cfg.roadNearZ, _cfg.roadFarZ, 40f, 0f);
            for (float x = -14f; x <= 14f; x += _cfg.dashSpacing) Quad("Dash", _cfg.dashColor, x, _cfg.dashZ - _cfg.dashHalfWidth, _cfg.dashZ + _cfg.dashHalfWidth, _cfg.dashHalfLength, 0.03f);
            for (int i = 0; i < 3; i++) BuildPMark(i);
        }

        // V17: khung đứt nét + chữ P ở ô bến (chữ P chỉ hiện khi ô trống)
        void BuildPMark(int slot)
        {
            var root = new GameObject("PMark" + slot); root.transform.SetParent(_root, false);
            root.transform.localPosition = SlotPos(slot) + new Vector3(0f, 0.02f, 0f); root.transform.localRotation = SlotRot;
            var mat = Mat("Custom/BuzzyEnvironment", _cfg.pMarkColor);
            float w = _cfg.pMarkSize.x, l = _cfg.pMarkSize.y, d = _cfg.pMarkDash, t = _cfg.pMarkLine;
            Action<Vector3, Vector3> seg = (c, sz) =>
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false); q.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                q.transform.localPosition = c; q.transform.localScale = new Vector3(sz.x, sz.z, 1f); q.GetComponent<MeshRenderer>().sharedMaterial = mat;
            };
            for (float z = -l * 0.5f + d * 0.5f; z <= l * 0.5f; z += d * 2f) { seg(new Vector3(-w * 0.5f, 0f, z), new Vector3(t, 0f, d)); seg(new Vector3(w * 0.5f, 0f, z), new Vector3(t, 0f, d)); }
            for (float x = -w * 0.5f + d * 0.5f; x <= w * 0.5f; x += d * 2f) { seg(new Vector3(x, 0f, -l * 0.5f), new Vector3(d, 0f, t)); seg(new Vector3(x, 0f, l * 0.5f), new Vector3(d, 0f, t)); }
            var pgo = new GameObject("P"); pgo.transform.SetParent(root.transform, false);
            pgo.transform.localPosition = new Vector3(0f, 0.03f, 0f); pgo.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var tm = pgo.AddComponent<TextMesh>(); tm.text = "P"; tm.font = _a.font; tm.fontSize = 64; tm.characterSize = _cfg.pLetterSize * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = _cfg.pMarkColor;
            var m2 = new Material(_a.font.material); m2.renderQueue = 2450; pgo.GetComponent<MeshRenderer>().sharedMaterial = m2;
            _pMark[slot] = pgo; pgo.SetActive(false);
        }

        // C3/C4: vào màn: khung phóng to từ giữa + màn đỏ tan dần
        void StartIntro()
        {
            _baseOrtho = _cam.orthographicSize;
            if (!_cam.orthographic || _cfg.introZoomDuration <= 0f) return;
            _cam.orthographicSize = _baseOrtho * _cfg.introZoom;
            if (_uiRoot != null) _uiRoot.localScale = Vector3.one / _cfg.introZoom;
            var go = new GameObject("Haze", typeof(RectTransform), typeof(Image)); go.transform.SetParent(_canvas.transform, false);
            _haze = go.GetComponent<Image>(); _haze.color = _cfg.introHazeColor; _haze.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void TickIntro()
        {
            if (_baseOrtho <= 0f || _cfg.introZoomDuration <= 0f) return;
            float u = Mathf.Clamp01(Time / _cfg.introZoomDuration), e = 1f - (1f - u) * (1f - u);
            float k = Mathf.Lerp(_cfg.introZoom, 1f, e);
            _cam.orthographicSize = _baseOrtho * k;
            if (_uiRoot != null) _uiRoot.localScale = Vector3.one / k;
            if (_haze != null)
            {
                float a = _cfg.introHazeColor.a * (1f - Mathf.Clamp01(Time / _cfg.introHazeDuration));
                _haze.color = new Color(_cfg.introHazeColor.r, _cfg.introHazeColor.g, _cfg.introHazeColor.b, a);
                if (a <= 0f) { Kill(_haze.gameObject); _haze = null; }
            }
        }

        readonly Dictionary<int, Material> _blockMats = new Dictionary<int, Material>();
        float _buildSeed;

        void BuildBoard()
        {
            var b = Game.Board;
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++)
            {
                int c = b.ColorAt(x, y); if (c == Board.Empty) continue;
                // dựng từ dưới-trái lan chéo lên phải (C5)
                float diag = ((float)x / Mathf.Max(1, _lv.width)) * 0.55f + ((float)y / Mathf.Max(1, _lv.height)) * 0.45f;
                _buildSeed += 0.37f;
                CreatePass(new PixelData(x, y, c), diag * _cfg.appearSpreadDuration.x + 0.04f * Mathf.Abs(Mathf.Sin(_buildSeed)));
            }
            MarkLooseClusters();
        }

        // V23 [thiết kế]: cụm không dính ảnh chính (thành phần liên thông nhỏ hơn) đứng tách ra: dịch vào tâm + xa camera, nhấp nhô nhẹ
        void MarkLooseClusters()
        {
            var b = Game.Board; var comp = new int[b.W, b.H]; int n = 0; var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++)
            {
                if (b.ColorAt(x, y) == Board.Empty || comp[x, y] != 0) continue;
                n++; int size = 0; stack.Push(x * 1000 + y); comp[x, y] = n;
                while (stack.Count > 0)
                {
                    int k = stack.Pop(), cx = k / 1000, cy = k % 1000; size++;
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + dx[d], ny = cy + dy[d];
                        if (!b.InBounds(nx, ny) || b.ColorAt(nx, ny) == Board.Empty || comp[nx, ny] != 0) continue;
                        comp[nx, ny] = n; stack.Push(nx * 1000 + ny);
                    }
                }
                sizes.Add(size);
            }
            if (n <= 1) return;
            int main = 1; for (int i = 1; i < sizes.Count; i++) if (sizes[i] > sizes[main - 1]) main = i + 1;
            float cx0 = _cfg.boardCenterX;
            foreach (var kv in _pass)
            {
                var p = kv.Value; int c = comp[p.d.X, p.d.Y];
                if (c == main || b.IsFrozen(p.d.X, p.d.Y)) continue;
                float dir = p.home.x < cx0 ? 1f : -1f;
                p.home += new Vector3(dir * _cfg.looseInward * _cfg.cellPitchX, 0f, _cfg.looseLift);
                p.loose = true; p.phase = c * 1.7f;
            }
        }

        Material PassMat(int color)
        {
            Material mat;
            if (!_blockMats.TryGetValue(color, out mat)) { mat = Mat("Custom/BuzzyPassenger", BuzzyPalette.Get(color)); _blockMats[color] = mat; }
            return mat;
        }

        Pass CreatePass(PixelData d, float appearAt)
        {
            var go = new GameObject("P");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.blockMesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = PassMat(d.Color);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(1.2f, 1.4f, 1.0f);
            var ps = new Pass { tr = go.transform, d = d, home = PixelPos(d), appearAt = appearAt, mr = mr };
            _pass[d.X * 1000 + d.Y] = ps;
            SetPassScale(ps, 0f);
            return ps;
        }

        Vector3 BlockScale { get { return new Vector3(_cfg.blockScale * _cfg.blockWidthScale, _cfg.blockScale * _cfg.blockHeightScale, _cfg.blockScale); } }

        void SetPassScale(Pass p, float s)
        {
            p.tr.localScale = BlockScale * s;
            p.tr.localPosition = p.home + new Vector3(0f, -1.2f * (1f - Mathf.Clamp01(s)), 0f);
            p.tr.localEulerAngles = new Vector3(0, 180f, 0);
        }

        CarV MakeCar(Car car, Vector3 pos, Quaternion rot, float scale)
        {
            var mat = Mat("Custom/BuzzyVehicle", BuzzyPalette.Get(car.Color)); mat.SetTexture("_BaseTex", _a.busTexture);
            var go = new GameObject("Car" + car.Id);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.busMesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = Vector3.one * scale;
            var cv = new CarV { car = car, tr = go.transform, mr = go.GetComponent<MeshRenderer>() };
            var bcol = go.AddComponent<BoxCollider>(); bcol.center = new Vector3(0f, 0.8f, 0f); bcol.size = new Vector3(1.7f, 1.6f, 2.8f);
            cv.texts = AddRoofNumber(go.transform, car.Remaining);
            if (car.Locked) AddLockOverlay(cv);
            _cars.Add(cv);
            return cv;
        }

        TextMesh[] AddRoofNumber(Transform carTr, int n)
        {
            var list = new List<TextMesh>();
            var root = new GameObject("Seats"); root.transform.SetParent(carTr, false);
            root.transform.localPosition = new Vector3(0f, 1.56f, 0.05f); root.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            Vector2[] offs = { new Vector2(0.024f, 0f), new Vector2(-0.024f, 0f), new Vector2(0f, 0.024f), new Vector2(0f, -0.024f) };
            foreach (var o in offs)
            {
                var sh = new GameObject("Outline"); sh.transform.SetParent(root.transform, false);
                sh.transform.localPosition = new Vector3(o.x, o.y, -0.02f);
                var t2 = MakeText(sh, n, new Color(0.1f, 0.1f, 0.15f, 1f), 2990); list.Add(t2);
            }
            list.Add(MakeText(root, n, Color.white, 3010));
            return list.ToArray();
        }

        TextMesh MakeText(GameObject go, int n, Color c, int queue)
        {
            var tm = go.AddComponent<TextMesh>();
            tm.text = n.ToString(); tm.font = _a.font; tm.fontSize = 64; tm.characterSize = _cfg.roofNumberSize * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            var m = new Material(_a.font.material); m.renderQueue = queue;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return tm;
        }

        void BuildCars()
        {
            // xe ở ô bến: Core đã Refill lúc dựng; xuất hiện từ phía trên (hầm), lăn xuống ô (C1)
            for (int i = 0; i < 3; i++)
            {
                var car = Game.Slots[i];
                if (car == null) continue;
                var cv = MakeCar(car, SlotPos(i) + new Vector3(0f, 0f, _cfg.enterFromZ), Quaternion.identity, _cfg.busScale);
                cv.col = i; cv.slotPos = SlotPos(i); _slotCar[i] = cv;
                StartMove(cv, new[] { cv.tr.localPosition, SlotPos(i) }, _cfg.enterDuration, SlotRot, true, i * _cfg.enterStagger);
            }
            for (int i = 0; i < 3; i++)
            {
                int k = 0;
                foreach (var car in Game.Queues[i])
                {
                    var cv = MakeCar(car, QueuePos(i, k) + new Vector3(0f, 0f, k == 0 ? _cfg.enterFromZ : 0f), Quaternion.identity, _cfg.queueBusScale * _cfg.busScale);
                    cv.col = i; _queue[i].Add(cv);
                    if (k == 0) StartMove(cv, new[] { cv.tr.localPosition, QueuePos(i, 0) }, _cfg.enterDuration, Quaternion.identity, true, _cfg.queueEnterDelay + i * _cfg.enterStagger);
                    k++;
                }
            }
        }

        void StartMove(CarV cv, Vector3[] pts, float dur, Quaternion endRot, bool useEnd, float delay, Action done = null)
        {
            var mv = new Move { pts = pts, dur = Mathf.Max(0.01f, dur), t = 0f, endRot = endRot, useEndRot = useEnd, delay = delay, done = done };
            mv.cum = new float[pts.Length]; for (int i = 1; i < pts.Length; i++) mv.cum[i] = mv.cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            cv.move = mv;
        }

        // ---------------------------------------------------------------- UI (theo % khung)
        void BuildUi()
        {
            var cv = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler)); cv.transform.SetParent(_root, false);
            _canvas = cv.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceCamera; _canvas.worldCamera = _cam; _canvas.planeDistance = 1f;
            var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = _canvasSize;
            var ur = new GameObject("UiRoot", typeof(RectTransform)); ur.transform.SetParent(cv.transform, false);
            _uiRoot = ur.GetComponent<RectTransform>(); _uiRoot.anchorMin = Vector2.zero; _uiRoot.anchorMax = Vector2.one; _uiRoot.offsetMin = _uiRoot.offsetMax = Vector2.zero;
            var ct = _uiRoot;
            Img("Tray", _a.tunnel, Color.white, ct, new Vector2(0.5f, _cfg.trayVisibleHeight * 0.5f - 0.11f + _cfg.trayShift), 1.06f);
            float barTop = _lv.tag > 0 ? _cfg.hardBottomBarTop : _cfg.bottomBarTop;
            Img("BottomBar", null, _lv.tag > 0 ? _cfg.hardBottomBarColor : _cfg.bottomBarColor, ct, new Vector2(0.5f, (1f + barTop) * 0.5f), 1f, (1f - barTop) * _canvasSize.y / _canvasSize.x);
            // U8: level khó / siêu khó dùng badge đầu lâu + thanh đáy đỏ
            var badgeSprite = _lv.tag == 1 ? _a.levelBarHard : _lv.tag == 2 ? _a.levelBarSuper : _a.levelBar;
            var badge = Img("LvBadge", badgeSprite, Color.white, ct, _lv.tag > 0 ? _cfg.badgeCenter + new Vector2(0f, _cfg.hardBadgeDropY) : _cfg.badgeCenter, _lv.tag > 0 ? _cfg.badgeWidth * _cfg.hardBadgeScale : _cfg.badgeWidth);
            Label("Lv." + _lv.index, badge, new Vector2(badge.sizeDelta.x * 0.5f, -badge.sizeDelta.y * (_lv.tag > 0 ? 0.6f : 0.38f)), (int)(52 * _canvasSize.x / 1080f), _lv.tag > 0 ? _cfg.hardBadgeTextColor : new Color32(0x4A, 0x45, 0x9A, 255));
            BuildBoosterBar(ct);
            BuildSpeedButton(ct);
            Img("Settings", _a.settings, Color.white, ct, _cfg.settingsCenter, _cfg.settingsSize);
            for (int i = 0; i < 3; i++)
            {
                var b = Img("Full" + i, _a.knob, new Color(0.72f, 0.75f, 0.95f, 1f), ct, _cfg.fullBubbleCenter, _cfg.fullBubbleWidth, 1f);
                Label("Full", b, new Vector2(b.sizeDelta.x * 0.5f, -b.sizeDelta.y * 0.45f), (int)(40 * _canvasSize.x / 1080f), new Color32(0x4A, 0x45, 0x9A, 255));
                b.gameObject.SetActive(false); _fullBubbles[i] = b;
            }
            Canvas.ForceUpdateCanvases();
        }

        RectTransform Img(string name, Sprite sprite, Color color, Transform parent, Vector2 pct, float widthPct, float? aspect = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.sprite = sprite; im.color = color; im.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            float w = widthPct * _canvasSize.x; float asp = aspect ?? (sprite != null ? sprite.rect.height / sprite.rect.width : 1f);
            rt.sizeDelta = new Vector2(w, w * asp); rt.anchoredPosition = new Vector2(pct.x * _canvasSize.x, -pct.y * _canvasSize.y);
            return rt;
        }

        Text Label(string text, Transform parent, Vector2 anchored, int size, Color c)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = text; t.font = _a.font; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.sizeDelta = new Vector2(260, 90); rt.anchoredPosition = anchored;
            return t;
        }

        // ---------------------------------------------------------------- sự kiện luật lõi
        CarV ViewOf(Car c) { foreach (var v in _cars) if (v.car == c) return v; return null; }

        void OnPixelArrived(Car car, PixelData p)
        {
            var v = ViewOf(car);
            if (v != null) foreach (var t in v.texts) t.text = Mathf.Max(0, car.Remaining).ToString();
        }

        void OnCarDeparted(Car car, int slot)
        {
            var cv = ViewOf(car); if (cv == null) return;
            cv.leaving = true; _slotCar[slot] = null;
            var s = SlotPos(slot);
            // xe rẽ phải, chạy ngược qua làn trên ra mép trái (C7)
            var pts = new[] { s, new Vector3(s.x + 3f, 0f, s.z + 1.5f), new Vector3(_cfg.leaveEdgeX, 0f, _cfg.leaveLaneZ), new Vector3(-_cfg.leaveEdgeX - 4f, 0f, _cfg.leaveLaneZ) };
            float len = 0f; for (int i = 1; i < pts.Length; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            Action go = () => StartMove(cv, pts, len / _cfg.leaveSpeed, Quaternion.identity, false, _cfg.leaveBrakePause, () => { _cars.Remove(cv); Kill(cv.tr.gameObject); });
            // C13: nhích lùi (giữ hướng) rồi dừng ngắn trước khi rẽ
            var back = s - cv.tr.localRotation * Vector3.forward * _cfg.leaveReverse;
            StartMove(cv, new[] { s, back }, _cfg.leaveReverseDuration, cv.tr.localRotation, true, 0.1f, go); cv.move.keepRot = true; pts[0] = back;
        }

        void OnCarEntered(Car car, int slot)
        {
            // chỉ xảy ra sau khi dựng: xe đầu cột xuống ô trống, các xe sau dồn lên (C3)
            if (_queue[slot].Count == 0) return;
            var cv = _queue[slot][0]; _queue[slot].RemoveAt(0);
            if (cv.car != car) { Log?.Invoke("CẢNH BÁO: xe vào ô không khớp hàng chờ"); return; }
            cv.slotPos = SlotPos(slot); _slotCar[slot] = cv;
            cv.tr.localScale = Vector3.one * _cfg.busScale;
            StartMove(cv, new[] { cv.tr.localPosition, SlotPos(slot) }, _cfg.queueToSlotDuration, SlotRot, true, 0.25f);
            for (int k = 0; k < _queue[slot].Count; k++)
                StartMove(_queue[slot][k], new[] { _queue[slot][k].tr.localPosition, QueuePos(slot, k) }, _cfg.queueToSlotDuration, Quaternion.identity, true, 0.35f);
        }

        void OnBarrier(int slot)
        {
            if (_a.barrierMesh == null) return;
            var go = new GameObject("Barrier" + slot); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.barrierMesh;
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, ((x + y) / 8) % 2 == 0 ? new Color32(0xE8, 0x2B, 0x2B, 255) : new Color32(0xF4, 0xF4, 0xF8, 255));
            tex.Apply();
            var m = Mat("Custom/BuzzyEnvironment", Color.white); m.mainTexture = tex; go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var s = SlotPos(slot);
            go.transform.localPosition = s + new Vector3(0.5f, 0f, 0f); go.transform.localEulerAngles = new Vector3(0, 0, 0);
            _pops.Add(new Pop { tr = go.transform, at = Time + _cfg.barrierDelay, dur = 0.3f, scale = Vector3.one * _cfg.barrierScale });
            go.transform.localScale = Vector3.zero;
        }

        // ---------------------------------------------------------------- thao tác
        /// <summary>Chạm cụm màu: pixel bay lên xe cùng màu; không có xe khớp thì rung (C11).</summary>
        public bool TapColor(int color)
        {
            if (Won || Time < _inputOpenAt) return false;
            var r = Game.Tap(color);
            if (!r.Matched) { if (Vibrate != null) Vibrate(); Log?.Invoke("t=" + Time.ToString("0.00") + " Tap màu " + color + " -> NoMatch (rung)"); return false; }
            TapCount++;
            Log?.Invoke("t=" + Time.ToString("0.00") + " Tap màu " + color + " -> " + r.Pixels.Count + " pixel lên xe #" + r.Car.Id + " (ô " + r.Slot + ")");
            StartFlights(r);
            return true;
        }

        void StartFlights(TapResult r, float delay = 0f)
        {
            var cv = ViewOf(r.Car);
            int n = r.Pixels.Count;
            float spread = Mathf.Min(_cfg.flightSpread, _cfg.flightSpread * n / 50f + 0.4f);
            for (int i = 0; i < n; i++)
            {
                Pass p; if (!_pass.TryGetValue(r.Pixels[i].X * 1000 + r.Pixels[i].Y, out p)) continue;
                p.flying = true;
                float jit = 1f + _cfg.walkSpeedJitter * ((float)_rnd.NextDouble() * 2f - 1f);
                _flights.Add(new Flight { p = p, car = cv, start = Time + delay + (n <= 1 ? 0f : (i / (float)(n - 1)) * spread), dur = _cfg.flightDuration * jit, from = p.home, phase = (float)_rnd.NextDouble() * 6.28f, side = _rnd.Next(2) == 0 ? -1f : 1f });
            }
        }

        // ---------------------------------------------------------------- vòng lặp
        void Update()
        {
            HandleInput();
            Tick(UnityEngine.Time.deltaTime * TimeScale);
        }

        public void Tick(float dt)
        {
            Time += dt;
            // pixel xuất hiện
            TickIntro();
            foreach (var kv in _pass)
            {
                var p = kv.Value; if (p.flying) continue;
                float u = Mathf.Clamp01((Time - p.appearAt) / _cfg.appearSpreadDuration.y);
                SetPassScale(p, EaseOutBack(u));
                if (p.loose && u >= 1f) p.tr.localPosition = p.home + new Vector3(0f, _cfg.looseBob * Mathf.Max(0f, Mathf.Sin(Time * _cfg.looseBobSpeed + p.phase)), 0f);   // nhảy lên rồi nghỉ
            }
            for (int i = 0; i < 3; i++)
            {
                if (_pMark[i] == null) continue;
                // V17: chữ P chỉ khi không còn xe nào trên ô (xe kế chưa đỗ xong, xe rời đã đi khỏi ô)
                bool show = (_slotCar[i] == null || _slotCar[i].move != null) && !Game.Barrier[i] && !Won;
                if (show) foreach (var c in _cars) if (Vector3.Distance(c.tr.localPosition, SlotPos(i)) < _cfg.pMarkSize.y * 0.6f) { show = false; break; }
                if (_pMark[i].activeSelf != show) _pMark[i].SetActive(show);
            }
            // pixel bay
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                float u = (Time - f.start) / f.dur;
                if (u < 0f) { f.p.tr.localPosition = f.p.home; continue; }
                var to = f.car.tr.position + new Vector3(0f, 1.4f, 0f);
                float e = Mathf.Clamp01(u); e = e * e * (3f - 2f * e);
                var flat = to - f.from; flat.y = 0f; var perp = Vector3.Cross(Vector3.up, flat.normalized);
                float wob = _cfg.walkWobble * Mathf.Sin(e * Mathf.PI * 2f + f.phase) * (1f - e) * f.side;
                float hop = _cfg.walkHop * Mathf.Abs(Mathf.Sin(e * Mathf.PI * _cfg.walkHops)) * (1f - e * 0.6f);
                var pos = Vector3.Lerp(f.from, to, e) + perp * wob + Vector3.up * (_cfg.flightArc * 4f * e * (1f - e) + hop);
                f.p.tr.localPosition = pos;
                f.p.tr.localScale = BlockScale * Mathf.Lerp(1f, 0.35f, e * e);
                if (flat.sqrMagnitude > 0.001f) f.p.tr.localRotation = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(0f, 180f + 25f * Mathf.Sin(e * 12f + f.phase), 0f);
                if (u >= 1f)
                {
                    _flights.RemoveAt(i);
                    Kill(f.p.tr.gameObject); ForgetPass(f.p);
                    Game.Arrive(f.car.car, f.p.d);
                }
            }
            // xe
            for (int i = _cars.Count - 1; i >= 0; i--) if (i < _cars.Count) StepCar(_cars[i], dt);
            // pop (rào)
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i]; float u = Mathf.Clamp01((Time - p.at) / p.dur);
                if (p.tr != null) p.tr.localScale = p.scale * EaseOutBack(u);
                if (u >= 1f) _pops.RemoveAt(i);
            }
            // bong bóng Full theo xe ở ô
            for (int i = 0; i < 3; i++)
            {
                var cv = _slotCar[i];
                bool show = cv != null && cv.car.IsFull && !cv.leaving && cv.move == null;
                if (cv != null && cv.car.IsFull && !cv.leaving) show = true;
                _fullBubbles[i].gameObject.SetActive(show);
                if (show)
                {
                    var vp = _cam.WorldToViewportPoint(cv.tr.position);
                    _fullBubbles[i].anchoredPosition = new Vector2((vp.x - 0.11f) * _canvasSize.x, -(1f - vp.y + 0.005f) * _canvasSize.y);
                }
            }
            TickMechanics(dt);
            // thắng
            if (!Won && Game.Status == GameStatus.Won && _flights.Count == 0 && _vanish.Count == 0)
            {
                bool anyLeaving = false; foreach (var c in _cars) if (c.leaving) anyLeaving = true;
                if (!anyLeaving) { Won = true; WonAt = Time; ShowWin(); }
            }
            CheckLose();
            TickPopups(dt);
        }

        void StepCar(CarV cv, float dt)
        {
            var mv = cv.move; if (mv == null) return;
            if (mv.delay > 0f) { mv.delay -= dt; return; }
            mv.t += dt;
            float u = Mathf.Clamp01(mv.t / mv.dur);
            float e = cv.leaving ? Mathf.Clamp01(u * 1.0f) : 1f - (1f - u) * (1f - u);   // vào ô: giảm tốc; rời: đều
            if (cv.leaving) e = u * u * (3f - 2f * u) * 0.35f + u * 0.65f;
            float total = mv.cum[mv.cum.Length - 1];
            float d = total * e;
            int seg = 1; while (seg < mv.pts.Length - 1 && mv.cum[seg] < d) seg++;
            float segLen = Mathf.Max(0.0001f, mv.cum[seg] - mv.cum[seg - 1]);
            float lu = Mathf.Clamp01((d - mv.cum[seg - 1]) / segLen);
            var dir = (mv.pts[seg] - mv.pts[seg - 1]).normalized;
            cv.tr.localPosition = Vector3.Lerp(mv.pts[seg - 1], mv.pts[seg], lu);
            Quaternion want = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-dir) : cv.tr.localRotation;
            if (mv.useEndRot && u > 0.7f) want = Quaternion.Slerp(want, mv.endRot, Mathf.InverseLerp(0.7f, 1f, u));
            if (!mv.keepRot) cv.tr.localRotation = Quaternion.Slerp(cv.tr.localRotation, want, 1f - Mathf.Exp(-10f * dt));
            if (u >= 1f)
            {
                cv.tr.localPosition = mv.pts[mv.pts.Length - 1];
                if (mv.useEndRot) cv.tr.localRotation = mv.endRot;
                cv.move = null;
                if (mv.done != null) mv.done();
            }
        }

        // màn thắng + popup: BuzzyGameView.Popups.cs (M5)

        static float EaseOutBack(float x)
        {
            if (x <= 0f) return 0f; if (x >= 1f) return 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
