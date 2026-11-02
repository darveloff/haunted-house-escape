using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the whole Haunted House game scene: map, lights, player, ghosts, keys, exit.
// Menu: Tools > Haunted House > Build Map
//
// Everything sits on a 4m cell grid (i = east, j = north in kit space).
// Rooms: small = 3x3 cells, wide = 5x3, large = 5x5. Each room has one doorway
// centred on every wall. Corridors are drawn as cell paths; the builder picks
// end / straight / corner / T / crossing pieces from each cell's connections.
// Any room doorway nothing connects to becomes a dead-end alcove.
public static class HauntedHouseMapBuilder
{
    const string KitPath = "Assets/Modular Space Kit 1.0/Models/FBX format/";
    const string CharPath = "Assets/Mini Characters/Models/FBX format/";
    const string OutPath = "Assets/HauntedHouse/";
    const string ScenePath = "Assets/Scenes/HauntedHouse.unity";
    const float Cell = 4f;

    const string PlayerModel = "character-female-b";
    const float CharacterScale = 2f;
    const float CharacterYaw = 0f; // set to 180 if characters walk backwards

    struct Room
    {
        public string name, model;
        public Vector2Int c;
        public int k;
        public Room(string name, string model, int i, int j, int k = 0) { this.name = name; this.model = model; c = new Vector2Int(i, j); this.k = k; }

        public Vector2Int Half
        {
            get
            {
                var h = model.StartsWith("room-large") ? new Vector2Int(2, 2)
                      : model.StartsWith("room-wide") ? new Vector2Int(2, 1)
                      : new Vector2Int(1, 1);
                return k % 2 == 0 ? h : new Vector2Int(h.y, h.x);
            }
        }
    }

    static readonly Room[] Rooms =
    {
        new Room("Foyer",       "room-large",           0,  0),
        new Room("Kitchen",     "room-wide",           -8,  0),
        new Room("Dining",      "room-wide-variation",  8,  0),
        new Room("Pantry",      "room-small",         -14,  0),
        new Room("Garage",      "room-small-variation", 14, 0),
        new Room("GreatHall",   "room-large",           0,  8),
        new Room("Library",     "room-large-variation", -8, 8),
        new Room("Ballroom",    "room-large",           8,  8),
        new Room("Bedroom",     "room-small",         -14,  8),
        new Room("Workshop",    "room-small-variation", 14, 8),
        new Room("Chapel",      "room-wide",            0, 16, 1),
        new Room("Nursery",     "room-small",          -8, 14),
        new Room("Observatory", "room-large-variation", 8, 16),
        new Room("Crypt",       "room-large",           0, 22),
    };

    // Corridor paths as cell polylines (axis-aligned segments). Endpoints are doorway cells.
    static readonly Vector2Int[][] Paths =
    {
        P(-3, 0, -5, 0),            // Foyer - Kitchen
        P(3, 0, 5, 0),              // Foyer - Dining
        P(-11, 0, -12, 0),          // Kitchen - Pantry
        P(11, 0, 12, 0),            // Dining - Garage
        P(0, 3, 0, 5),              // Foyer - Great Hall
        P(-3, 8, -5, 8),            // Great Hall - Library
        P(3, 8, 5, 8),              // Great Hall - Ballroom
        P(-8, 2, -8, 5),            // Kitchen - Library
        P(8, 2, 8, 5),              // Dining - Ballroom
        P(-11, 8, -12, 8),          // Library - Bedroom
        P(11, 8, 12, 8),            // Ballroom - Workshop
        P(-14, 2, -14, 6),          // Pantry - Bedroom
        P(14, 2, 14, 6),            // Garage - Workshop
        P(0, 11, 0, 13),            // Great Hall - Chapel
        P(-8, 11, -8, 12),          // Library - Nursery
        P(8, 11, 8, 13),            // Ballroom - Observatory
        P(-6, 14, -4, 14, -4, 16, -2, 16), // Nursery - Chapel
        P(2, 16, 5, 16),            // Chapel - Observatory
        P(-14, 10, -14, 14, -10, 14), // Bedroom - Nursery
        P(14, 10, 14, 16, 11, 16),  // Workshop - Observatory
        P(0, 19, 0, 19),            // Chapel - Crypt (shared doorway cell)
    };

    static readonly Vector2Int ExitCell = new Vector2Int(0, -3); // Foyer south doorway
    static readonly Vector2 PlayerSpawn = new Vector2(0, -1.2f);

    // One ghost per route; kinds line up with GhostRoutes.
    static readonly GhostKind[] GhostKinds =
    {
        GhostKind.Wisp, GhostKind.Wisp, GhostKind.Wraith, GhostKind.Watcher, GhostKind.Wraith, GhostKind.Screamer,
    };

    static readonly Dictionary<GhostKind, string> GhostKindModels = new Dictionary<GhostKind, string>
    {
        { GhostKind.Wisp, "character-male-c" },
        { GhostKind.Wraith, "character-female-e" },
        { GhostKind.Watcher, "character-male-f" },
        { GhostKind.Screamer, "character-female-c" },
    };

    // Filled while building: every place a key may appear (KeySpawner picks some each run).
    static readonly List<Vector2> keySpots = new List<Vector2>();

    static readonly Vector2[][] GhostRoutes =
    {
        new[] { new Vector2(-8, 0), new Vector2(-14, 0), new Vector2(-14, 8), new Vector2(-8, 8) },
        new[] { new Vector2(8, 0), new Vector2(14, 0), new Vector2(14, 8), new Vector2(8, 8) },
        new[] { new Vector2(0, 8), new Vector2(-8, 8), new Vector2(-8, 14), new Vector2(0, 16), new Vector2(8, 16), new Vector2(8, 8) },
        new[] { new Vector2(0, 22), new Vector2(0, 16), new Vector2(8, 16), new Vector2(14, 16), new Vector2(14, 10) },
        new[] { new Vector2(-14, 14), new Vector2(-8, 14), new Vector2(0, 16), new Vector2(-8, 14) },
        new[] { new Vector2(0, 21), new Vector2(1, 22), new Vector2(0, 23), new Vector2(-1, 22) },
    };


    static Vector2Int[] P(params int[] xy)
    {
        var r = new Vector2Int[xy.Length / 2];
        for (int n = 0; n < r.Length; n++) r[n] = new Vector2Int(xy[n * 2], xy[n * 2 + 1]);
        return r;
    }

    // Directions in CCW order: E, N, W, S. Bit d = open towards Dirs[d].
    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    // Base openings of each corridor piece at k = 0.
    static readonly (string model, int mask)[] CorridorPieces =
    {
        ("corridor-end", 0b0001),          // E
        ("corridor", 0b0101),              // E W
        ("corridor-corner", 0b1100),       // W S
        ("corridor-junction", 0b1101),     // E W S
        ("corridor-intersection", 0b1111),
    };

    // How kit x/z map into Unity x/z after FBX import. Detected at build time.
    static Vector2 mapX = new Vector2(1, 0);
    static Vector2 mapZ = new Vector2(0, 1);
    static float unitScale = 1f;

    [MenuItem("Tools/Haunted House/Build Map")]
    public static void BuildMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder("Assets/HauntedHouse")) AssetDatabase.CreateFolder("Assets", "HauntedHouse");
        controllers.Clear();
        colliderMeshes.Clear();
        keySpots.Clear();
        AssetDatabase.DeleteAsset(OutPath + "Colliders");
        AssetDatabase.CreateFolder("Assets/HauntedHouse", "Colliders");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (!DetectKitOrientation())
        {
            Debug.LogError("HauntedHouse: could not find the Modular Space Kit FBX files at " + KitPath);
            return;
        }

        var map = new GameObject("Map");
        BuildRooms(map.transform);
        BuildCorridors(map.transform);
        BuildProps(map.transform);
        BuildExit(map.transform);
        BakeNavMesh(map);
        BuildExitZone();

        BuildLighting();
        new GameObject("GameManager").AddComponent<GameManager>().fxMaterial = FxSpriteMaterial();
        int charactersLayer = SetupSilhouettes();
        var player = BuildPlayer();
        BuildGhosts();
        // Player and ghosts on their own layer so the silhouette pass can draw them behind walls.
        SetLayerRecursive(player, charactersLayer);
        foreach (var ghost in Object.FindObjectsByType<GhostAI>(FindObjectsSortMode.None))
            SetLayerRecursive(ghost.gameObject, charactersLayer);
        BuildKeys();
        SetupCamera(player.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild();
        Debug.Log($"HauntedHouse: built {Rooms.Length} rooms and saved {ScenePath}");
    }

    // ---------------------------------------------------------------- map

    static void BuildRooms(Transform map)
    {
        var parent = new GameObject("Rooms").transform;
        parent.SetParent(map);
        foreach (var r in Rooms)
        {
            var go = SpawnKit(r.model, (Vector2)r.c * Cell, r.k, parent);
            go.name = r.name;
            AddNavFloor(map, r.c, r.Half);
        }
    }

    // The NavMesh bake skips most of the kit's own floor triangles, so every room and
    // corridor cell gets a thin invisible box whose top sits 2cm above the visual floor.
    static void AddNavFloor(Transform map, Vector2Int cell, Vector2Int half)
    {
        var floors = map.Find("NavFloor");
        if (floors == null)
        {
            floors = new GameObject("NavFloor").transform;
            floors.SetParent(map);
        }
        var go = new GameObject($"Floor_{cell.x}_{cell.y}");
        go.transform.SetParent(floors);
        go.transform.position = CellToWorld(cell, -0.08f);
        go.isStatic = true;
        var box = go.AddComponent<BoxCollider>();
        Vector2 size = new Vector2(half.x * 2 + 1, half.y * 2 + 1) * Cell;
        Vector2 s = size.x * new Vector2(Mathf.Abs(mapX.x), Mathf.Abs(mapX.y)) + size.y * new Vector2(Mathf.Abs(mapZ.x), Mathf.Abs(mapZ.y));
        box.size = new Vector3(s.x, 0.2f, s.y);
    }

    static void BuildCorridors(Transform map)
    {
        var cells = new Dictionary<Vector2Int, int>();
        void Link(Vector2Int cell, int dir)
        {
            cells.TryGetValue(cell, out int m);
            cells[cell] = m | (1 << dir);
        }

        // Every doorway cell opens back into its room.
        foreach (var r in Rooms)
        {
            var h = r.Half;
            for (int d = 0; d < 4; d++)
            {
                var dir = Dirs[d];
                var door = r.c + new Vector2Int(dir.x * (h.x + 1), dir.y * (h.y + 1));
                Link(door, (d + 2) % 4);
            }
        }

        foreach (var path in Paths)
        {
            for (int n = 0; n + 1 < path.Length; n++)
            {
                var a = path[n];
                var b = path[n + 1];
                var step = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
                if (step == Vector2Int.zero) continue;
                int d = System.Array.IndexOf(Dirs, step);
                for (var c = a; c != b; c += step)
                {
                    Link(c, d);
                    Link(c + step, (d + 2) % 4);
                }
            }
        }

        var parent = new GameObject("Corridors").transform;
        parent.SetParent(map);
        foreach (var kv in cells)
        {
            AddNavFloor(map, kv.Key, Vector2Int.zero);
            foreach (var r in Rooms)
            {
                var h = r.Half;
                if (Mathf.Abs(kv.Key.x - r.c.x) <= h.x && Mathf.Abs(kv.Key.y - r.c.y) <= h.y)
                    Debug.LogWarning($"HauntedHouse: corridor cell {kv.Key} overlaps room {r.name}");
            }

            if (!PickCorridor(kv.Value, out string model, out int k))
            {
                Debug.LogWarning($"HauntedHouse: no corridor piece for cell {kv.Key} mask {kv.Value}");
                continue;
            }
            SpawnKit(model, (Vector2)kv.Key * Cell, k, parent);

            // Dead ends (except the exit) are candidate key spots, tucked against the back wall.
            int open = -1;
            for (int d = 0; d < 4; d++) if (kv.Value == 1 << d) open = d;
            if (open >= 0 && kv.Key != ExitCell)
            {
                var spot = (Vector2)kv.Key * Cell - (Vector2)Dirs[open] * 0.8f;
                keySpots.Add(spot);
            }
        }
    }

    // ---------------------------------------------------------------- cover props

    static readonly Vector2Int[] Quadrants = { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1) };

    // Freestanding walls, pillars and corner nooks in each room quadrant, clear of the
    // doorway lanes through the room centre. They give cover to break line of sight.
    static void BuildProps(Transform map)
    {
        var parent = new GameObject("Props").transform;
        parent.SetParent(map);

        for (int n = 0; n < Rooms.Length; n++)
        {
            var r = Rooms[n];
            bool large = r.model.StartsWith("room-large");
            bool wide = r.model.StartsWith("room-wide");
            // Variation rooms (except wide) have their NE and SW quadrants cut away.
            bool cutCorners = r.model.EndsWith("-variation") && !wide;
            Vector2 q = large ? new Vector2(5f, 5f) : wide ? new Vector2(5.5f, 2.8f) : new Vector2(3f, 3f);

            for (int qi = 0; qi < 4; qi++)
            {
                var s = Quadrants[qi];
                if (cutCorners && s.x == s.y) continue;
                Vector2 local = new Vector2(s.x * q.x, s.y * q.y);
                int variant = (n + qi) % 3;

                if (large)
                {
                    if (variant == 0)
                    {
                        // Nook walls face the room centre; you slip in from the room corner side.
                        // Its pocket is a candidate key spot.
                        int nookK = s.x > 0 ? (s.y > 0 ? 2 : 1) : (s.y > 0 ? 3 : 0);
                        Prop(parent, r, "template-corner", local, nookK);
                        var nook = (Vector2)r.c * Cell + RotateKit(local + RotateKit(new Vector2(-0.6f, -0.6f), nookK), r.k);
                        keySpots.Add(nook);
                    }
                    else if (variant == 1)
                    {
                        Prop(parent, r, "template-wall", local, s.x * s.y > 0 ? 0 : 1);
                        Prop(parent, r, "template-detail", local + new Vector2(s.x * 2.5f, -s.y * 2.5f), 0);
                    }
                    else
                    {
                        Prop(parent, r, "template-wall-half", local + new Vector2(0, s.y), 0);
                        Prop(parent, r, "template-wall-half", local + new Vector2(s.x, 0), 1);
                        Prop(parent, r, "template-detail", local - new Vector2(s.x * 1.6f, s.y * 1.6f), 0);
                    }
                }
                else if (wide)
                {
                    if (variant == 2)
                    {
                        Prop(parent, r, "template-detail", local + new Vector2(1.5f, 0), 0);
                        Prop(parent, r, "template-detail", local - new Vector2(1.5f, 0), 0);
                    }
                    else Prop(parent, r, variant == 0 ? "template-wall" : "template-wall-detail-a", local, 0);
                }
                else
                {
                    if (variant == 1) Prop(parent, r, "template-detail", local, 0);
                    else Prop(parent, r, "template-wall-half", local, s.x * s.y > 0 ? 0 : 1);
                }
            }
        }
    }

    static void Prop(Transform parent, Room room, string model, Vector2 local, int k)
    {
        SpawnKit(model, (Vector2)room.c * Cell + RotateKit(local, room.k), (k + room.k) % 4, parent);
    }

    static Vector2 RotateKit(Vector2 v, int k)
    {
        for (int i = 0; i < k; i++) v = new Vector2(-v.y, v.x);
        return v;
    }

    static bool PickCorridor(int mask, out string model, out int k)
    {
        foreach (var piece in CorridorPieces)
        {
            for (k = 0; k < 4; k++)
            {
                int rotated = ((piece.mask << k) | (piece.mask >> (4 - k))) & 0b1111;
                if (rotated == mask)
                {
                    model = piece.model;
                    return true;
                }
            }
        }
        model = null;
        k = 0;
        return false;
    }

    static ExitDoor BuildExit(Transform map)
    {
        // Gate sits on the boundary between the Foyer and its south doorway cell.
        Vector2 gatePos = ((Vector2)ExitCell + new Vector2(0, 0.5f)) * Cell;
        var gate = SpawnKit("gate-door", gatePos, 0, map);
        gate.name = "ExitDoor";
        foreach (var t in gate.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;

        var lightGo = new GameObject("ExitLight");
        lightGo.transform.SetParent(gate.transform, true);
        lightGo.transform.position = ToWorld((Vector2)ExitCell * Cell, 3f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.15f, 0.1f);
        light.range = 8f;
        light.intensity = 4f;

        var door = gate.AddComponent<ExitDoor>();
        door.door = gate.transform.Find("door");
        door.statusLight = light;
        return door;
    }

    // Created after the NavMesh bake so the trigger box doesn't end up in it.
    static void BuildExitZone()
    {
        var zone = new GameObject("ExitZone");
        zone.transform.position = ToWorld((Vector2)ExitCell * Cell + new Vector2(0, -0.5f), 1.5f);
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2.5f, 3f, 2.5f);
        zone.AddComponent<ExitZone>();
    }

    // Kit corridors only have ~1.5m of clear floor between the wall bases, so the default
    // Humanoid agent (radius 0.5) erodes them away. Size the agent for the mini characters.
    static void ConfigureNavAgent()
    {
        var settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").FirstOrDefault();
        if (settingsAsset == null) return;
        var so = new SerializedObject(settingsAsset);
        var humanoid = so.FindProperty("m_Settings").GetArrayElementAtIndex(0);
        humanoid.FindPropertyRelative("agentRadius").floatValue = 0.25f;
        humanoid.FindPropertyRelative("agentHeight").floatValue = 1.5f;
        humanoid.FindPropertyRelative("agentClimb").floatValue = 0.4f;
        humanoid.FindPropertyRelative("minRegionArea").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BakeNavMesh(GameObject map)
    {
        ConfigureNavAgent();
        // Bake inside an explicit volume that reaches below the floor. With the default
        // bounds the floors sit exactly on the bottom edge (y = 0) and get dropped.
        var renderers = map.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        var surface = map.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.center = new Vector3(b.center.x, 2f, b.center.z) - map.transform.position;
        surface.size = new Vector3(b.size.x + 4f, 8f, b.size.z + 4f);
        surface.BuildNavMesh();

        const string path = OutPath + "HauntedHouse_NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }

    // template-wall-corner sits at x[-1,0] z[-1,0] and template-wall at x[-2,2] z[-1,0]
    // in the source model. Measuring them in Unity tells us any axis flip/swap.
    static bool DetectKitOrientation()
    {
        var wall = Measure(KitPath + "template-wall.fbx");
        var corner = Measure(KitPath + "template-wall-corner.fbx");
        if (wall == null || corner == null) return false;

        Bounds w = wall.Value, c = corner.Value;
        bool swapped = w.size.z > w.size.x;
        unitScale = Mathf.Max(w.size.x, w.size.z) / 4f;
        if (!swapped)
        {
            mapX = new Vector2(c.center.x < 0 ? 1 : -1, 0);
            mapZ = new Vector2(0, w.center.z < 0 ? 1 : -1);
        }
        else
        {
            mapZ = new Vector2(w.center.x < 0 ? 1 : -1, 0);
            mapX = new Vector2(0, c.center.z < 0 ? 1 : -1);
        }
        return true;
    }

    static Bounds? Measure(string assetPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = Vector3.zero;
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        Object.DestroyImmediate(go);
        return b;
    }

    static Vector3 ToWorld(Vector2 kitPos, float y = 0)
    {
        Vector2 u = kitPos.x * mapX + kitPos.y * mapZ;
        return new Vector3(u.x, y, u.y);
    }

    static Vector3 CellToWorld(Vector2 cell, float y = 0) => ToWorld(cell * Cell, y);

    static GameObject SpawnKit(string model, Vector2 pos, int k, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath + model + ".fbx");
        if (prefab == null)
        {
            Debug.LogWarning("HauntedHouse: missing model " + model);
            return new GameObject(model + " (missing)");
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = ToWorld(pos);
        go.transform.rotation = Quaternion.Euler(0, KitTurnToUnityYaw(k), 0) * prefab.transform.rotation;
        if (!Mathf.Approximately(unitScale, 1f))
            go.transform.localScale = prefab.transform.localScale / unitScale;

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = ColliderMesh(model, mf);
        foreach (var t in go.GetComponentsInChildren<Transform>())
            t.gameObject.isStatic = true;
        return go;
    }

    // The kit floors are double-sided: each floor triangle has a downward-facing twin
    // at the same height, which makes the NavMesh bake treat the floor as unwalkable.
    // Colliders use a copy of the mesh with the downward faces stripped out.
    static readonly Dictionary<Mesh, Mesh> colliderMeshes = new Dictionary<Mesh, Mesh>();

    static Mesh ColliderMesh(string model, MeshFilter mf)
    {
        var src = mf.sharedMesh;
        if (colliderMeshes.TryGetValue(src, out var cached)) return cached;

        var verts = src.vertices;
        var kept = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tris = src.GetTriangles(s);
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 n = mf.transform.TransformDirection(Vector3.Cross(b - a, c - a)).normalized;
                if (n.y < -0.9f) continue;
                kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
            }
        }

        var mesh = new Mesh { name = $"{model}_{mf.name}_collider" };
        mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.SetTriangles(kept, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, OutPath + "Colliders/" + mesh.name + ".asset");
        colliderMeshes[src] = mesh;
        return mesh;
    }

    // Converts a counter-clockwise kit quarter turn into a Unity yaw,
    // accounting for whatever axis flip the FBX import introduced.
    static float KitTurnToUnityYaw(int k)
    {
        float a = k * 90f * Mathf.Deg2Rad;
        float r00 = Mathf.Cos(a), r01 = -Mathf.Sin(a), r10 = Mathf.Sin(a), r11 = Mathf.Cos(a);
        float m00 = mapX.x, m01 = mapZ.x, m10 = mapX.y, m11 = mapZ.y;
        float i00 = m00, i01 = m10, i10 = m01, i11 = m11; // M^-1 = M^T
        float t00 = r00 * i00 + r01 * i10, t01 = r00 * i01 + r01 * i11;
        float t10 = r10 * i00 + r11 * i10, t11 = r10 * i01 + r11 * i11;
        float u00 = m00 * t00 + m01 * t10, u01 = m00 * t01 + m01 * t11;
        return Mathf.Round(Mathf.Atan2(u01, u00) * Mathf.Rad2Deg);
    }

    // ---------------------------------------------------------------- lighting

    static readonly Color[] RoomColors =
    {
        new Color(1f, 0.7f, 0.4f), new Color(0.55f, 1f, 0.6f), new Color(0.7f, 0.5f, 1f),
        new Color(0.45f, 0.6f, 1f), new Color(1f, 0.45f, 0.35f), new Color(0.6f, 0.9f, 0.9f),
    };

    static void BuildLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.08f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.02f);
        RenderSettings.fogDensity = 0.03f;

        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.name = "Moonlight";
            sun.intensity = 0.1f;
            sun.color = new Color(0.55f, 0.65f, 1f);
            sun.transform.rotation = Quaternion.Euler(60, -30, 0);
        }

        var lights = new GameObject("Lights").transform;
        for (int n = 0; n < Rooms.Length; n++)
        {
            var r = Rooms[n];
            var go = new GameObject("Light_" + r.name);
            go.transform.SetParent(lights);
            go.transform.position = CellToWorld(r.c, 3.8f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RoomColors[n % RoomColors.Length];
            light.range = Mathf.Max(r.Half.x, r.Half.y) * Cell * 1.6f;
            light.intensity = 3f;
            go.AddComponent<LightFlicker>();
        }
    }

    // ---------------------------------------------------------------- silhouettes

    const string CharactersLayerName = "Characters";

    static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // Adds a "Characters" layer and, to every URP renderer in the project, Render Objects passes
    // that draw characters as a soft silhouette wherever a wall hides them. Each character type
    // gets a mask pass (stencil-marks its visible pixels) and a silhouette pass (depth Greater,
    // skipping marked pixels) so a character never shows a silhouette over its own body.
    static readonly string[] SilhouettePassNames = { "PlayerMask", "PlayerSilhouette", "GhostMask", "GhostSilhouette" };

    static int SetupSilhouettes()
    {
        int layer = EnsureLayer(CharactersLayerName);
        if (layer < 0)
        {
            Debug.LogWarning("HauntedHouse: no free layer for Characters; silhouettes skipped.");
            return 0;
        }

        var maskMat = SilhouetteMaterial("Silhouette_Mask", new Color(0f, 0f, 0f, 0f));
        var playerMat = SilhouetteMaterial("Silhouette_Player", new Color(1f, 1f, 1f, 0.45f));
        var ghostMat = SilhouetteMaterial("Silhouette_Ghost", new Color(0.65f, 0.85f, 1f, 0.3f));
        var opaque = UnityEngine.Rendering.Universal.RenderQueueType.Opaque;
        var transparent = UnityEngine.Rendering.Universal.RenderQueueType.Transparent;
        var afterOpaques = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingOpaques;
        var afterTransparents = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingTransparents;

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;

            // Rebuild our passes from scratch so their order is always right.
            foreach (var old in data.rendererFeatures.Where(f => f != null && SilhouettePassNames.Contains(f.name)).ToList())
            {
                data.rendererFeatures.Remove(old);
                Object.DestroyImmediate(old, true);
            }
            AddPass(data, "PlayerMask", opaque, afterOpaques, layer, maskMat, CompareFunction.LessEqual, CompareFunction.Always, StencilOp.Replace);
            AddPass(data, "PlayerSilhouette", opaque, afterOpaques, layer, playerMat, CompareFunction.Greater, CompareFunction.NotEqual, StencilOp.Keep);
            AddPass(data, "GhostMask", transparent, afterTransparents, layer, maskMat, CompareFunction.LessEqual, CompareFunction.Always, StencilOp.Replace);
            AddPass(data, "GhostSilhouette", transparent, afterTransparents, layer, ghostMat, CompareFunction.Greater, CompareFunction.NotEqual, StencilOp.Keep);

            // The renderer keeps a parallel list of its features' local file IDs; resync it.
            var so = new SerializedObject(data);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int n = 0; n < data.rendererFeatures.Count; n++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[n], out string _, out long localId);
                map.GetArrayElementAtIndex(n).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        return layer;
    }

    static void AddPass(UnityEngine.Rendering.Universal.UniversalRendererData data, string name,
        UnityEngine.Rendering.Universal.RenderQueueType queue, UnityEngine.Rendering.Universal.RenderPassEvent evt,
        int layer, Material mat, CompareFunction depthTest, CompareFunction stencilTest, StencilOp stencilPass)
    {
        var feature = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.RenderObjects>();
        feature.name = name;
        var st = feature.settings;
        st.passTag = name;
        st.Event = evt;
        st.filterSettings.RenderQueueType = queue;
        st.filterSettings.LayerMask = 1 << layer;
        st.overrideMaterial = mat;
        st.overrideMaterialPassIndex = 0;
        st.overrideDepthState = true;
        st.depthCompareFunction = depthTest;
        st.enableWrite = false;
        st.stencilSettings.overrideStencilState = true;
        st.stencilSettings.stencilReference = 1;
        st.stencilSettings.stencilCompareFunction = stencilTest;
        st.stencilSettings.passOperation = stencilPass;
        st.stencilSettings.failOperation = StencilOp.Keep;
        st.stencilSettings.zFailOperation = StencilOp.Keep;
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
    }

    static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
            layers.GetArrayElementAtIndex(i).stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        return -1;
    }

    // Flat see-through colour, no lighting.
    static Material SilhouetteMaterial(string name, Color color)
    {
        string path = OutPath + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- characters

    static GameObject BuildPlayer()
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = CellToWorld(PlayerSpawn, 0.1f);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.3f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.67f, 0);
        cc.stepOffset = 0.3f;

        var model = SpawnCharacter(PlayerModel, player.transform, null);
        var pc = player.AddComponent<PlayerController>();
        pc.animator = model.GetComponent<Animator>();

        // Lantern so the player can see in the dark corridors.
        var lantern = new GameObject("Lantern").AddComponent<Light>();
        lantern.transform.SetParent(player.transform, false);
        lantern.transform.localPosition = new Vector3(0, 2.5f, 0.3f);
        lantern.type = LightType.Point;
        lantern.color = new Color(1f, 0.85f, 0.6f);
        lantern.range = 9f;
        lantern.intensity = 2.5f;
        lantern.shadows = LightShadows.Soft;
        return player;
    }

    static void BuildGhosts()
    {
        var root = new GameObject("Ghosts").transform;
        for (int g = 0; g < GhostRoutes.Length; g++)
        {
            var route = GhostRoutes[g];
            var ghost = new GameObject($"Ghost_{g + 1}");
            ghost.transform.SetParent(root);
            ghost.transform.position = CellToWorld(route[0]);

            var agent = ghost.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.4f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.2f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(ghost.transform, false);
            var kind = GhostKinds[g % GhostKinds.Length];
            var profile = GhostAI.Profiles[(int)kind];
            ghost.name = $"Ghost_{g + 1}_{profile.name}";
            string modelName = GhostKindModels[kind];
            var model = SpawnCharacter(modelName, visual, GhostMaterial(modelName, kind));
            model.transform.localScale *= kind == GhostKind.Watcher ? 1.3f : kind == GhostKind.Wraith ? 1f : 1.1f;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(ghost.transform, false);
            glow.transform.localPosition = new Vector3(0, 1.5f, 0);
            glow.type = LightType.Point;
            glow.color = profile.color;
            glow.range = 5f;
            glow.intensity = 3f;

            var waypointRoot = new GameObject("Route").transform;
            waypointRoot.SetParent(root);
            waypointRoot.name = $"Ghost_{g + 1}_Route";
            var points = new Transform[route.Length];
            for (int n = 0; n < route.Length; n++)
            {
                var wp = new GameObject("WP_" + n).transform;
                wp.SetParent(waypointRoot);
                wp.position = CellToWorld(route[n]);
                points[n] = wp;
            }

            var ai = ghost.AddComponent<GhostAI>();
            ai.kind = kind;
            ghost.AddComponent<GhostFX>().spriteMaterial = FxSpriteMaterial();
            ai.waypoints = points;
            ai.visual = visual;
            ai.animator = model.GetComponent<Animator>();
            ai.glow = glow;
        }
    }

    static GameObject SpawnCharacter(string modelName, Transform parent, Material overrideMaterial)
    {
        string fbx = CharPath + modelName + ".fbx";
        ConfigureCharacterImport(fbx);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0, CharacterYaw, 0) * prefab.transform.localRotation;
        model.transform.localScale = prefab.transform.localScale * CharacterScale;

        foreach (var col in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = GetController(modelName, fbx);
        animator.applyRootMotion = false;

        if (overrideMaterial != null)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = Enumerable.Repeat(overrideMaterial, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        return model;
    }

    static readonly string[] LoopClips = { "idle", "walk", "sprint" };

    static void ConfigureCharacterImport(string fbx)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (importer == null) return;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (var c in clips)
        {
            if (LoopClips.Any(n => ClipMatches(c.name, n)) && !c.loopTime)
            {
                c.loopTime = true;
                changed = true;
            }
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static bool ClipMatches(string clipName, string wanted) =>
        clipName == wanted || clipName.EndsWith("|" + wanted) || clipName.EndsWith("/" + wanted);

    static readonly Dictionary<string, AnimatorController> controllers = new Dictionary<string, AnimatorController>();

    static AnimatorController GetController(string modelName, string fbx)
    {
        if (controllers.TryGetValue(modelName, out var cached) && cached != null) return cached;

        string path = OutPath + modelName + ".controller";
        AssetDatabase.DeleteAsset(path);

        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Find(string n)
        {
            var clip = clips.FirstOrDefault(c => ClipMatches(c.name, n));
            if (clip == null)
                Debug.LogWarning($"HauntedHouse: clip '{n}' not found in {modelName}. Available: {string.Join(", ", clips.Select(c => c.name))}");
            return clip;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(Find("idle"), 0f);
        tree.AddChild(Find("walk"), 1f);
        tree.AddChild(Find("sprint"), 2f);
        var locomotion = sm.AddState("Locomotion");
        locomotion.motion = tree;
        sm.defaultState = locomotion;

        var die = sm.AddState("Die");
        die.motion = Find("die");
        var toDie = sm.AddAnyStateTransition(die);
        toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        toDie.hasExitTime = false;
        toDie.duration = 0.1f;
        toDie.canTransitionToSelf = false;

        AssetDatabase.SaveAssets();
        controllers[modelName] = controller;
        return controller;
    }

    // Walks from the Foyer centre to the Great Hall centre and reports floor height / navmesh along the way.
    [MenuItem("Tools/Haunted House/Probe Doorway")]
    static void ProbeDoorway()
    {
        DetectKitOrientation();
        var sb = new System.Text.StringBuilder("Probe Foyer->GreatHall\n");
        for (float z = 0; z <= 32; z += 0.5f)
        {
            Vector3 p = ToWorld(new Vector2(0, z));
            string floor = Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 10f)
                ? $"{hit.point.y:F2} ({hit.collider.name})" : "none";
            bool nav = NavMesh.SamplePosition(p, out var nh, 1.5f, NavMesh.AllAreas);
            Physics.queriesHitBackfaces = true;
            string above = Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, out var up, 10f)
                ? $"{up.point.y:F2} ({up.collider.name}, n={up.normal.y:F1})" : "none";
            var downHits = Physics.RaycastAll(p + Vector3.up * 6f, Vector3.down, 10f)
                .OrderBy(h => -h.point.y).Select(h => $"{h.point.y:F2}/{h.normal.y:F1}/{h.collider.name}");
            Physics.queriesHitBackfaces = false;
            sb.AppendLine($"z {z,5}: floor {floor}  nav {(nav ? $"{nh.position} d={Vector3.Distance(nh.position, p):F2}" : "-")}  above {above}  all [{string.Join(", ", downHits)}]");
        }
        var tri = NavMesh.CalculateTriangulation();
        sb.AppendLine($"NavMesh verts {tri.vertices.Length}, tris {tri.indices.Length / 3}");
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface != null)
        {
            var s = NavMesh.GetSettingsByID(surface.agentTypeID);
            sb.AppendLine($"Agent radius {s.agentRadius} height {s.agentHeight} climb {s.agentClimb} slope {s.agentSlope} data {(surface.navMeshData != null)}");
        }
        Debug.Log(sb.ToString());
    }

    // Play-mode shortcuts for checking screens without playing a full round.
    [MenuItem("Tools/Haunted House/Debug/Start Game")] static void DebugStart() => GameManager.Instance?.StartGame();
    [MenuItem("Tools/Haunted House/Debug/Pause")] static void DebugPause() => GameManager.Instance?.Pause();
    [MenuItem("Tools/Haunted House/Debug/Caught By Wraith")] static void DebugCaught() => GameManager.Instance?.LoseCaught(GhostKind.Wraith);
    [MenuItem("Tools/Haunted House/Debug/Escaped")] static void DebugEscaped() => GameManager.Instance?.Win();
    [MenuItem("Tools/Haunted House/Debug/Collect Key")] static void DebugCollectKey() => GameManager.Instance?.CollectKey();
    // Records frames for the README GIF (run after Debug > Start Game). Keeps the editor
    // ticking while it records, since it barely updates when it's in the background.
    [MenuItem("Tools/Haunted House/Debug/Record Gameplay GIF Frames")]
    static void DebugRecordGif()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (!EditorApplication.isPlaying || player == null) return;
        player.gameObject.AddComponent<DemoPilot>();
        void Pump()
        {
            if (!EditorApplication.isPlaying || player == null || player.GetComponent<DemoPilot>() == null)
            {
                EditorApplication.update -= Pump;
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        EditorApplication.update += Pump;
    }

    // Puts the player just inside the Foyer's south wall, where the wall hides them from the camera.
    [MenuItem("Tools/Haunted House/Debug/Stand Behind Wall")]
    static void DebugStandBehindWall()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(-3f, 0.1f, -8.6f);
        cc.enabled = true;
    }
    [MenuItem("Tools/Haunted House/Debug/Preview Phone Controls")]
    static void DebugTogglePhoneControls()
    {
        bool on = PlayerPrefs.GetInt("hh_preview_touch", 0) == 0;
        PlayerPrefs.SetInt("hh_preview_touch", on ? 1 : 0);
        Debug.Log($"HauntedHouse: phone controls preview {(on ? "ON" : "OFF")} (takes effect next Play)");
    }
    [MenuItem("Tools/Haunted House/Debug/Reset Best Time + Tutorial")]
    static void DebugResetSave()
    {
        PlayerPrefs.DeleteKey("hh_best_time");
        PlayerPrefs.DeleteKey("hh_best_stars");
        PlayerPrefs.DeleteKey("hh_tutorial_done");
        Debug.Log("HauntedHouse: best time, stars and tutorial reset");
    }

    [MenuItem("Tools/Haunted House/Debug/Log Canvases")]
    static void LogCanvases()
    {
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            Debug.Log($"Canvas {c.name} active={c.gameObject.activeInHierarchy} flags={c.gameObject.hideFlags} scene={c.gameObject.scene.name} enabled={c.enabled}");
    }

    [MenuItem("Tools/Haunted House/Capture Game View")]
    static void CaptureGameView()
    {
        // When the editor is in the background it barely ticks, so push frames through
        // first (lets UI animations finish), then capture.
        double start = EditorApplication.timeSinceStartup;
        bool captured = false;
        void Tick()
        {
            if (EditorApplication.isPlaying) UIAnim.FinishAll();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            double elapsed = EditorApplication.timeSinceStartup - start;
            if (!captured && elapsed > 2.5)
            {
                ScreenCapture.CaptureScreenshot("Logs/GameView.png");
                captured = true;
            }
            if (elapsed > 3.0)
            {
                EditorApplication.update -= Tick;
                Debug.Log("HauntedHouse: captured Logs/GameView.png");
            }
        }
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Haunted House/Log Ghost Status")]
    static void LogGhostStatus()
    {
        foreach (var ai in Object.FindObjectsByType<GhostAI>(FindObjectsSortMode.None))
        {
            var agent = ai.GetComponent<NavMeshAgent>();
            var anim = ai.animator;
            Debug.Log($"{ai.name}: pos {ai.transform.position} onNavMesh {agent.isOnNavMesh} vel {agent.velocity.magnitude:F2} " +
                      $"path {agent.pathStatus} remaining {(agent.isOnNavMesh ? agent.remainingDistance : -1):F1} " +
                      $"controller {(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NONE")}");
        }
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) Debug.Log($"Player at {player.transform.position}, grounded {player.GetComponent<CharacterController>().isGrounded}");
    }

    // ---------------------------------------------------------------- keys

    static void BuildKeys()
    {
        var root = new GameObject("Keys").transform;
        root.gameObject.AddComponent<KeySpawner>();
        var gold = KeyMaterial();
        for (int n = 0; n < keySpots.Count; n++)
        {
            var key = new GameObject($"KeySpot_{n + 1}");
            key.transform.SetParent(root);
            key.transform.position = ToWorld(keySpots[n], 1.1f);

            var trigger = key.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;
            key.AddComponent<KeyPickup>();

            // Simple key built from primitives: ring + shaft + two teeth.
            Part(key.transform, PrimitiveType.Cylinder, gold, new Vector3(0, 0.35f, 0), new Vector3(90, 0, 0), new Vector3(0.45f, 0.04f, 0.45f));
            Part(key.transform, PrimitiveType.Cube, gold, new Vector3(0, -0.1f, 0), Vector3.zero, new Vector3(0.1f, 0.7f, 0.1f));
            Part(key.transform, PrimitiveType.Cube, gold, new Vector3(0.12f, -0.35f, 0), Vector3.zero, new Vector3(0.18f, 0.08f, 0.1f));
            Part(key.transform, PrimitiveType.Cube, gold, new Vector3(0.1f, -0.2f, 0), Vector3.zero, new Vector3(0.14f, 0.07f, 0.1f));

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(key.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.3f);
            light.range = 4f;
            light.intensity = 3f;
        }
    }

    static void Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ---------------------------------------------------------------- materials

    // Unlit sprite material for ghost effects. Saved as an asset so the shader ships in builds.
    static Material FxSpriteMaterial()
    {
        string path = OutPath + "FX_Sprite.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material KeyMaterial()
    {
        string path = OutPath + "KeyGold.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.25f));
        mat.SetFloat("_Metallic", 0.8f);
        mat.SetFloat("_Smoothness", 0.7f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.7f, 0.45f, 0.05f));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material GhostMaterial(string modelName, GhostKind kind)
    {
        string path = OutPath + "Ghost_" + kind + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Color tint = GhostAI.Profiles[(int)kind].color;

        // Keep the character's own texture so faces stay readable, but tint and fade it.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPath + modelName + ".fbx");
        var source = prefab.GetComponentInChildren<Renderer>().sharedMaterial;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (source != null && source.mainTexture != null) mat.SetTexture("_BaseMap", source.mainTexture);
        Color baseColor = Color.Lerp(tint, Color.white, 0.35f);
        baseColor.a = 0.55f;
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", tint * 0.6f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------------------------------------------------------------- camera / build settings

    static void SetupCamera(Transform player)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.farClipPlane = 120f;
        var follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player;

        // Hear the world from the player, not from the camera 14m up.
        var camListener = cam.GetComponent<AudioListener>();
        if (camListener != null) Object.DestroyImmediate(camListener);
        var ears = new GameObject("Listener", typeof(AudioListener)).transform;
        ears.position = player.position + Vector3.up * 1.2f;
        follow.ears = ears;
        // Saved camera pose matches the title-screen framing, so the Game view looks the same
        // before you press Play. CameraFollow takes over from here at runtime.
        cam.transform.position = player.position + new Vector3(0f, 26f, -24f);
        cam.transform.LookAt(player.position + Vector3.forward * 20f);
    }

    static void AddSceneToBuild()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
