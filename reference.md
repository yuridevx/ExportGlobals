# Exile-API Reference

## 1. Memory Reading (`IMemory`)

Access via `GameController.Memory` or `RemoteMemoryObject.M`.

### Core Read Methods
```csharp
// Read struct/primitive at address
T Read<T>(long addr);
T Read<T>(long addr, int[] offsets);  // With pointer chain

// Read bytes
byte[] ReadBytes(long addr, int size);
byte[] ReadMem(long addr, int size);

// Read strings
string ReadString(long addr, int length, bool replaceNull);
string ReadStringU(long addr, int lengthBytes, bool replaceNull);  // Unicode
string ReadNativeString(long addr);  // Native std::string

// Read collections
T[] ReadStdVector<T>(StdVector nativeContainer);
IList<T> ReadStdList<T>(IntPtr head);
T[] ReadMem<T>(long addr, int size);  // Array of T
```

### Process Properties
```csharp
long AddressOfProcess { get; }
IntPtr OpenProcessHandle { get; }
Process Process { get; }
```

---

## 2. Entity System

### Entity Class (`ExileCore.PoEMemory.MemoryObjects.Entity`)

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `Address` | `long` | Memory address |
| `Id` | `uint` | Unique entity ID |
| `Path` / `Metadata` | `string` | Entity type path |
| `Pos` / `PosNum` | `Vector3` | World position |
| `GridPos` / `GridPosNum` | `Vector2` | Grid position |
| `Type` | `EntityType` | Monster, Chest, Player, etc. |
| `Rarity` | `MonsterRarity` | Normal, Magic, Rare, Unique |
| `IsValid`, `IsAlive`, `IsHostile` | `bool` | State flags |
| `DistancePlayer` | `float` | Distance to player |
| `Buffs` | `List<Buff>` | Active buffs |
| `Stats` | `Dictionary<GameStat, int>` | Entity stats |

**Component Access:**
```csharp
T GetComponent<T>() where T : Component;
bool TryGetComponent<T>(out T component);
bool HasComponent<T>();
```

### Memory Addresses

All game objects inherit from `RemoteMemoryObject`, which provides the `Address` property.

**RemoteMemoryObject (Base Class):**
```csharp
public abstract class RemoteMemoryObject
{
    long Address { get; }              // Memory address of this object
    IMemory M { get; }                 // Memory interface for reading

    T GetObject<T>(long address);      // Create object at address
    T ReadObject<T>(long addrPointer); // Read pointer, then create object
    T GetObjectAt<T>(int offset);      // Create object at Address + offset
}
```

**Entity Address & Component Lookup:**
```csharp
// Entity address
long entityAddr = entity.Address;

// Component addresses are cached in a dictionary (component name -> address)
Dictionary<string, long> CacheComp { get; }

// Raw component list (native vector of component pointers)
StdVector ComponentList { get; }

// Get component address from cache
if (entity.CacheComp.TryGetValue("Life", out long lifeAddr))
{
    // lifeAddr is the memory address of the Life component
}
```

**Component Base Class:**
```csharp
public class Component : RemoteMemoryObject
{
    long Address { get; }        // Component's memory address (inherited)
    long OwnerAddress { get; }   // Address of owning entity
    Entity Owner { get; }        // Reference to owning entity
}
```

**Reading Raw Memory from Addresses:**
```csharp
// Get component and its address
var life = entity.GetComponent<Life>();
long lifeAddr = life.Address;

// Read raw data at component address
var memory = GameController.Memory;
byte[] rawData = memory.ReadBytes(lifeAddr, 0x100);

// Read struct at offset from component
int curHp = memory.Read<int>(lifeAddr + 0x10);

// Components also expose M for memory access
var data = life.M.Read<MyStruct>(life.Address + offset);
```

### Common Components

| Component | Key Properties |
|-----------|----------------|
| `Life` | `CurHP`, `MaxHP`, `HPPercentage`, `CurES`, `MaxES`, `CurMana` |
| `Render` | `Pos`, `Bounds`, `Height`, `Name`, `TerrainHeight` |
| `Positioned` | `GridPos`, `WorldPos`, `Rotation`, `Scale` |
| `Actor` | `ActorSkills`, `Animation`, `isMoving`, `isAttacking`, `DeployedObjects` |
| `Chest` | `IsOpened`, `IsLocked`, `IsStrongbox` |
| `ObjectMagicProperties` | `Mods`, `Rarity` |
| `Buffs` | `BuffsList` |

### Iterating Entities

Access via `GameController`:
```csharp
// All entities
ICollection<Entity> entities = GameController.Entities;

// Via EntityListWrapper
EntityListWrapper wrapper = GameController.EntityListWrapper;
List<Entity> valid = wrapper.OnlyValidEntities;
Dictionary<EntityType, List<Entity>> byType = wrapper.ValidEntitiesByType;
Entity player = wrapper.Player;

// Get by ID
Entity entity = EntityListWrapper.GetEntityById(id);
```

**Plugin Entity Events:**
```csharp
public override void EntityAdded(Entity entity) { }
public override void EntityRemoved(Entity entity) { }
```

### EntityType Enum
```csharp
Monster = 100, Chest = 101, Npc = 102, Shrine = 103,
AreaTransition = 104, Portal = 105, Player = 109,
WorldItem = 111, TownPortal = 118, Door = 129, ...
```

---

## 3. Graphics (`ExileCore.Graphics`)

Access via `BaseSettingsPlugin.Graphics`.

### Shapes - Screen Space
```csharp
// Boxes & Frames
void DrawBox(RectangleF rect, Color color, float rounding = 0);
void DrawBox(Vector2 p1, Vector2 p2, Color color, float rounding = 0);
void DrawFrame(RectangleF rect, Color color, int thickness);

// Circles & Ellipses
void DrawCircle(Vector2 center, float radius, Color color, float thickness = 1);
void DrawCircleFilled(Vector2 center, float radius, Color color, int segments);
void DrawEllipse(Vector2 center, Vector2 radius, Color color, float rotation, int segments);

// Lines & Polygons
void DrawLine(Vector2 p1, Vector2 p2, float width, Color color);
void DrawPolyLine(Vector2[] points, Color color, float thickness);
void DrawConvexPolyFilled(Vector2[] points, Color color);
void DrawQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color);
```

### Shapes - World Space (3D -> Screen projection)
```csharp
void DrawCircleInWorld(Vector3 worldCenter, float radius, Color color, float thickness = 1, int segments = 32, bool followTerrain = false);
void DrawFilledCircleInWorld(Vector3 worldCenter, float radius, Color color, int segments = 32, bool followTerrain = false);
void DrawLineInWorld(Vector2 p1, Vector2 p2, float width, Color color);
void DrawBoundingBoxInWorld(Vector3 position, Color color, Vector3 bounds, float rotationRadians, float height);
void DrawBoundingCylinderInWorld(Vector3 position, Color color, Vector3 bounds, float rotationRadians);
```

### Shapes - Large Map
```csharp
void DrawCircleOnLargeMap(Vector2 gridPos, bool followTerrain, float radius, Color color, float thickness = 1);
void DrawFilledCircleOnLargeMap(Vector2 gridPos, bool followTerrain, float radius, Color color);
void DrawLineOnLargeMap(Vector2 p1, Vector2 p2, float width, Color color);
```

### Text
```csharp
Vector2 DrawText(string text, Vector2 position, Color color, FontAlign align = Left);
Vector2 DrawText(string text, Vector2 position, Color color, int height, FontAlign align);
Vector2 DrawTextWithBackground(string text, Vector2 position, Color color, Color bgColor);
Vector2 MeasureText(string text);
IDisposable SetTextScale(float scale);
```

### Images
```csharp
void DrawImage(string fileName, RectangleF rectangle, Color color);
void DrawImage(AtlasTexture atlasTexture, RectangleF rectangle);
bool InitImage(string name, string path);
bool HasImage(string name);
IntPtr GetTextureId(string name);
```

---

## 4. Coordinate Transformation

### Camera (`IngameState.Camera`)
```csharp
Camera camera = GameController.IngameState.Camera;

// World -> Screen conversion
Vector2 screenPos = camera.WorldToScreen(worldPos3D);

// Camera properties
Vector3 Position { get; }
Vector2 Size { get; }
float ActualZoomLevel { get; }
```

---

## 5. Plugin Integration

### BaseSettingsPlugin Structure
```csharp
public class MyPlugin : BaseSettingsPlugin<MySettings>
{
    // Injected by framework
    public GameController GameController { get; }
    public Graphics Graphics { get; }

    public override bool Initialise() { return true; }

    public override void Render()
    {
        // Drawing code here - called every frame
    }

    public override void EntityAdded(Entity entity) { }
    public override void EntityRemoved(Entity entity) { }
    public override void AreaChange(AreaInstance area) { }
}
```

### Visualization Example
```csharp
public override void Render()
{
    var camera = GameController.IngameState.Camera;

    foreach (var entity in GameController.Entities)
    {
        if (!entity.IsValid || entity.Type != EntityType.Monster)
            continue;

        // Get world position from Render component
        var render = entity.GetComponent<Render>();
        if (render == null) continue;

        // Convert to screen coordinates
        var screenPos = camera.WorldToScreen(render.PosNum);

        // Draw circle at entity position
        Graphics.DrawCircleFilled(screenPos, 10, Color.Red, 12);

        // Draw HP if alive
        var life = entity.GetComponent<Life>();
        if (life != null)
        {
            Graphics.DrawText($"{life.HPPercentage:P0}", screenPos, Color.White);
        }

        // Draw circle in world space (ground marker)
        Graphics.DrawCircleInWorld(render.PosNum, 20f, Color.Yellow, 2f);
    }
}
```

### Key Access Paths
```
GameController
├── Memory (IMemory)           -> Raw memory reading
├── Entities                   -> All entities
├── EntityListWrapper          -> Filtered entity lists
├── Player                     -> Player entity shortcut
├── IngameState
│   ├── Camera                 -> WorldToScreen conversion
│   ├── Data (IngameData)      -> Game data
│   └── IngameUi               -> UI elements
├── Area                       -> Current area info
└── Game (TheGame)             -> Root game object

Entity
├── Address                    -> Memory address
├── GetComponent<T>()          -> Component access
├── Pos / PosNum               -> World position
├── GridPos / GridPosNum       -> Grid position
└── Type, IsValid, IsAlive...  -> State
```
