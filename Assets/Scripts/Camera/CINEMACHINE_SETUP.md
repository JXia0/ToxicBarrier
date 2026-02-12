# Cinemachine 摄像机跟踪设置说明

本项目的摄像机由 **Cinemachine** 驱动，自动跟随 Tag 为 `Player` 的对象。按下面步骤即可完成设置。

---

## 一、确认已安装 Cinemachine

1. 打开 **Window → Package Manager**。
2. 左上角选择 **Unity Registry**，在列表中找到 **Cinemachine**。
3. 若未安装，点击 **Install**；若已安装可忽略（本项目 `Packages/manifest.json` 中已加入 `com.unity.cinemachine`，重新打开工程后会自动解析）。

---

## 二、场景内基础设置

### 1. Main Camera

- 保留场景中的 **Main Camera**（Tag 为 `MainCamera`）。
- 在其 GameObject 上添加组件：**Cinemachine Brain**（菜单：**Component → Cinemachine → Cinemachine Brain**）。
  - Cinemachine Brain 会控制该 Camera 的 position/rotation，根据当前激活的 Virtual Camera 来更新画面。

### 2. 创建 Virtual Camera（跟随玩家）

1. 在 Hierarchy 中 **右键 → Cinemachine → Virtual Camera**（或 **2D Camera**，若是 2D 项目）。
2. 将生成的 Virtual Camera 放在合适位置（例如与 Main Camera 同级或单独一个空物体下）。
3. 选中该 Virtual Camera，在 Inspector 中：
   - **Follow**：拖入场景中的 **Player** 物体（即你要跟随的对象）。
   - **Look At**（若有）：2D 顶视角可不设置，或同样设为 Player。
4. 若使用 **Cinemachine 2D**：
   - 在 Virtual Camera 上会有 **Cinemachine Extension** 或 **2D** 相关设置，保持默认即可，必要时可调 **Damping** 让跟随更平滑。

### 3. 使用本项目的自动绑定脚本（可选）

项目中提供了 `CinemachineFollowPlayer.cs`，可在运行时自动把「玩家」赋给 Virtual Camera 的 Follow，无需在编辑器里拖引用：

1. 在场景中创建一个空物体（如命名为 `CameraSetup`）。
2. 挂上 **CinemachineFollowPlayer** 组件。
3. 组件中：
   - **Follow Target**：留空则自动用 Tag 为 `Player` 的对象。
   - **Virtual Camera Component**：留空则自动用场景中第一个 Cinemachine Virtual Camera。
4. 确保玩家 GameObject 的 **Tag 为 `Player`**（Player 预制体通常已设置）。

这样无论玩家是场景里已有还是运行时生成，只要 Tag 正确，摄像机都会自动跟随。

---

## 三、常用参数（Virtual Camera / 2D）

在 Virtual Camera 的 Body 等模块中可调：

| 参数 | 说明 |
|------|------|
| **Damping (X/Y)** | 跟随的平滑程度，越大越平滑、延迟越明显。 |
| **Dead Zone** | 玩家在屏幕中心附近多大范围内不移动摄像机，可减少抖动。 |
| **Soft Zone** | 玩家偏离中心多少时开始缓慢移动摄像机。 |

2D 顶视角一般只需调 **Follow** 的 Damping 即可获得舒适感。

---

## 四、视角缩进 / 拉远（缩放）

要让画面**缩进一点**（看得更近、显示范围更小）：

- **方式一（推荐）**：在挂有 **CinemachineFollowPlayer** 的物体上，设置 **Orthographic Size**：填一个大于 0 的数值（例如 `4` 或 `3`），数值**越小**画面越近。
- **方式二**：选中 **Main Camera**，在 Camera 组件里把 **Size** 调小（同上，越小越近）。
- **方式三**：选中 **Cinemachine Virtual Camera**，在 **Lens** 里设置 **Orthographic Size** 调小。

常用参考：默认很多 2D 项目是 5；想缩进一点可试 **4** 或 **3.5**。

---

## 五、简要检查清单

- [ ] Package Manager 中已安装 **Cinemachine**。
- [ ] **Main Camera** 上有 **Cinemachine Brain**。
- [ ] 场景中有一个 **Cinemachine Virtual Camera**（或 2D Camera）。
- [ ] Virtual Camera 的 **Follow** 已设为 Player（或交给 `CinemachineFollowPlayer` 自动绑定）。
- [ ] 玩家 GameObject 的 **Tag** 为 `Player`。

完成以上步骤后，运行游戏即可看到摄像机跟随玩家移动。

---

## 六、相机边界（逐环解锁）— 只看到已解锁的 Map

**目标**：Cinemachine 只显示**已解锁**的 Map 区域，未解锁的 Map 里**看不到任何内容**（相机不进入、地图视觉与内容可隐藏）。

### 6.1 相机被「挡」在当前解锁范围内

1. 在 **MapController**（或任意常驻物体）上添加组件 **Camera Bounds From Map Unlock**（已挂好则可略过）。
2. **Virtual Camera Component** 留空时，脚本会自动查找场景中的 Cinemachine Virtual Camera。
3. 运行后脚本会为 Virtual Camera 添加 **Cinemachine Confiner 2D**，并用 **PolygonCollider2D** 近似圆形作为 Bounding Shape，半径随当前解锁环更新：
   - 只解锁 Map 1 → 相机只能在 Map 1 圆内移动，**看不到 Map 2/3 区域**；
   - 解锁 Map 2 → 边界扩大到 Map 2；
   - 满级解锁 Map 3 → 边界扩大到 Map 3。

这样相机**只能看到已解锁的 Map**，不会把镜头移进未解锁区域。

### 6.2 未解锁 Map 不显示任何内容（MapZone）

在 **Map 2、Map 3** 的 **MapZone** 组件上：

- **Hide Map Visual When Locked**（默认勾选）：锁定该环时隐藏本环的**地图圈 Sprite**，只显示战争迷雾；解锁后显示地图圈、隐藏迷雾。这样未解锁环不会露出「地图图形」。
- **Content Root To Hide When Locked**（可选）：拖入该环下的一个子物体（例如专门放刷怪/资源的容器）。锁定该环时该物体（及子物体）会被 **SetActive(false)**，未解锁环内的**内容**（刷怪、资源等）也不会被看到。

配合上面的 Confiner，Cinemachine 里就只会看到已解锁 Map，且未解锁 Map 里的 feature 不会显示。
