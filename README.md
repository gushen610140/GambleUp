# GambleUp

**Gamble With Your Friends** 的增强模组，基于 [MelonLoader](https://melonloader.xyz/)。

当前版本解决一个具体问题：**没钱的时候，可以狂按老虎机打工赚钱。** 不扣注、不转轴、每次按按钮直接到账固定金额。

在这个过渡方案之上，后续会逐步替换为真正的经济系统。

## 功能与规划

| 状态 | 功能 | 说明 |
| --- | --- | --- |
| **已完成** | 老虎机打工机 | 在赌场楼层指定坐标生成任意数量的老虎机，每次按按钮直接发放固定余额，不进入下注 / 转轴 / 派奖流程。 |
| 计划中 | 独立经济系统 | 把玩家钱包从赌场原有账本中剥离，由 GambleUp 自行记录、结算玩家的收支，而不是继续复用原版 `MoneyManager` 流程。 |
| 计划中 | 玩家借贷系统 | 玩家可抵押余额或物品借款，按游戏内天数计息，到期结算或违约。依赖上一项独立经济系统。 |

## 运行要求

- Windows 10 / 11，64 位。
- 正版 **Gamble With Your Friends**（Steam）。
- **MelonLoader**。本模组依赖它，**游戏本身不带**，需要自行安装一次，步骤见下文。

## 安装教程

总共需要两个东西：先装 MelonLoader，再放模组 DLL。

### 第一步：安装 MelonLoader

Gamble With Your Friends 没有自带 Mod 加载器，必须先装 MelonLoader，否则游戏不会加载 `Mods` 目录里的任何东西。

1. 前往 MelonLoader 官网：**<https://melonloader.xyz/>**
   - 需要更详细的说明、版本变更记录和常见问题，去官网看，不要用第三方转载的教程。
   - 源码与各版本下载：<https://github.com/LavaGang/MelonLoader/releases>
2. 下载最新版本的 MelonLoader（Windows 64 位）。
3. 运行安装程序。它会要求你选择游戏所在的目录，选到下面这个文件夹（有 `Gamble With Your Friends.exe` 的那一层）：

   ```
   ...\steamapps\common\Gamble With Your Friends\
   ```

   游戏目录可以这样快速定位：Steam 库里右键 **Gamble With Your Friends** -> **管理** -> **浏览本地文件**，Steam 会直接打开它。

4. 安装完成。游戏目录里应该多出这些东西：

   ```
   <游戏目录>\
       Gamble With Your Friends.exe
       Gamble With Your Friends_Data\
       version.dll              <- MelonLoader 装出来的引导代理
       MelonLoader\             <- MelonLoader 本体
       Mods\                    <- 模组放这里
   ```

5. **验证一下**：通过 Steam 正常启动游戏。如果出现一个黑色的 MelonLoader 控制台窗口，滚动到最后一行显示 `MelonLoader 0.7.3` 之类的版本号，就说明装好了。

   > 如果没看到控制台窗口，说明安装没成功。先别往下走，回头检查第 3 步选的目录对不对。

### 第二步：安装 GambleUp

1. 前往 **Releases** 页面下载最新的 `GambleUp-*.zip`：
   <https://github.com/gushen610140/GambleUp/releases>
2. 解压，里面是：

   ```
   GambleUp-1.1.0.zip
       Mods\
           GambleUp.dll
       GambleUp_cheat.example.txt
   ```

3. 把 `GambleUp.dll` 复制到游戏目录的 `Mods` 文件夹里。

   最终结构必须是：

   ```
   <游戏目录>\Mods\GambleUp.dll
   ```

   > **注意：** DLL 要直接放在 `Mods` 里，不要再套子文件夹。除非你同时在子文件夹里放了 `manifest.json`，否则 MelonLoader 不会递归扫描。

4. **没有 `Mods` 文件夹怎么办**：在游戏目录里右键 -> **新建** -> **文件夹**，名字必须正好是 `Mods`。MelonLoader 安装程序正常情况下会自己建这个目录。

5. 通过 Steam 正常启动游戏。进赌场楼层（`CasinoScene`）后会看到 GambleUp 的启动横幅：

   ```
   ================================================================
     GambleUp probe online
   ================================================================
   ```

到这里就装完了。配置是可选的，见下一节。

## 配置（可选）

GambleUp 每次生成机器时都会重新读取配置，**改完直接生效，不需要重新编译或重装**。

配置文件路径（不存在就自己新建）：

```
%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends\GambleUp_cheat.txt
```

`%LOCALAPPDATA%` 一般就是 `C:\Users\<你的用户名>\AppData\Local`。按 `Win + R`，粘贴
`%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends` 可以直接跳到该目录。

内容示例：

```ini
# GambleUp 配置
# 格式为 "键 = 值"，# 开头是注释

enabled = true

# 每按一次按钮，给交互的玩家增加多少余额。可以写小数，例如 reward = 2.5
reward = 5

# 一行一台机器。机器朝向跟随你当前面朝的方向，并反转 180 度。
pos = -6, 0, 70
pos = -3, 0, 70
pos = 3, 0, 70
pos = 6, 0, 70
```

| 键 | 含义 |
| --- | --- |
| `enabled` | `true` / `false`。设为 `false` 时 GambleUp 不生成任何东西。 |
| `reward` | 每次按按钮到账的余额，支持小数。 |
| `pos` | 一台机器的世界坐标 `x, y, z`。**要几台就写几行**。也可以写成一行、用 `;` 分隔，例如 `pos = -6,0,70; -3,0,70`。把所有 `pos` 行都删掉，则改为在角色正前方 2.5 米处生成一台。 |

配置在赌场楼层加载时读取，所以关掉游戏改完文件再进游戏即可验证。

## 联机说明

**只有房主需要安装 GambleUp，客户端不需要。**

游戏基于 [Mirror](https://mirror-networking.gitbook.io/docs/) 网络框架，采用房主权威（host-authoritative）模型：

- 机器通过 `NetworkServer.Spawn` 生成，而该调用只在 `NetworkServer.active` 为 true 的地方生效，也就是房主。客户端上模组会自行跳过。
- 机器生成后，Mirror 会自动把它们同步给所有客户端，因此所有人看到的机器数量和位置都一致。
- `MoneyManager` 里的 `balance` 和 `ticketBalance` 是 SyncVar，在房主修改后会同步到所有玩家。因此**队友也能按这些机器并正常拿到钱**。
- 唯一的例外是**专用服务器（dedicated server）**架构：这时 DLL 要放在服务器那台机器上，而不是某个玩家的客户端上。

另外，模组是通过反射读取游戏内部私有字段实现的（例如 `GameStamp.gamePrefab`），**游戏更新后字段一旦改名，模组就会失效**。联机时所有人的游戏版本需要一致。

## 日志

详细日志写在游戏旁边：

```
%LOCALAPPDATA%\Low\TENSTACK\Gamble With Your Friends\GambleUp_probe.log
```

内容包括进入场景的记录、每台机器的生成坐标，以及每 2 秒一次的玩家位置 / 朝向 / 余额 / 票数实时快照。排查问题先看这个文件，报告 bug 时也请附上它。

嫌刷屏的话，在同一目录新建一个空文件 `GambleUp_probe.paused` 即可暂停这些实时输出，删掉该文件恢复。关键事件的横幅不受影响，始终会打印。

## 从源码编译

需要：能编译 `netstandard2.1` 目标的 [.NET SDK](https://dotnet.microsoft.com/download)，以及本机已安装好的游戏和 MelonLoader。

```powershell
git clone https://github.com/gushen610140/GambleUp.git
cd GambleUp
```

然后告诉构建脚本你的游戏目录在哪，三种方式任选其一：

```powershell
# 1) 单次指定
dotnet build -c Release -p:GamePath="D:\Steam\steamapps\common\Gamble With Your Friends"

# 2) 当前终端会话生效
$env:GAMBLEUP_GAME_PATH = "D:\Steam\steamapps\common\Gamble With Your Friends"
dotnet build -c Release

# 3) 永久生效（推荐，省得每次都写）
[Environment]::SetEnvironmentVariable("GAMBLEUP_GAME_PATH", "D:\Steam\steamapps\common\Gamble With Your Friends", "User")
```

都不设置时，默认回退到 `C:\Program Files (x86)\Steam\steamapps\common\Gamble With Your Friends`。

路径不对时构建会**直接报一条清晰的错误**并说明怎么修，而不是抛出一堆"找不到命名空间"。

编译完成后 DLL 会**自动复制**到 `<游戏目录>\Mods`，也就是"编译"和"安装"一步到位。只想编译不想部署：

```powershell
dotnet build -c Release -p:DeployMod=false
```

> 本项目所有程序集引用都指向游戏目录下的 `Gamble With Your Friends_Data\Managed\*.dll` 和 `MelonLoader\net35\*.dll`，所以**必须先装好游戏和 MelonLoader 才能编译成功**。

## 项目结构

```
GambleUp.sln
GambleUp/
  Core.cs                  MelonMod 入口，Harmony 初始化已移除，场景钩子
  Probe.cs                 日志、横幅、玩家状态实时广播
  Cheat.cs                 机器生成、交互事件替换、到账逻辑
  GambleUp.csproj          netstandard2.1 / x64，引用游戏程序集
  Directory.Build.props    解析 $(GamePath)
```

## 实现原理

给想读源码的人：

1. **场景判定**：只按场景名 `CasinoScene` 工作，其他场景一律忽略。
2. **获取 prefab**：遍历场景里所有 `GameStamp` 组件，读它们的私有字段 `gamePrefab`，取第一个带 `Slots` 组件的。这正是游戏自己 `GameStamp.SpawnGame` 用的那个 prefab。
   - *不能* 直接克隆场景里已经存在的机器：`hasSpawned` 是 `NetworkIdentity` 的私有字段，`Object.Instantiate` 场景对象会把 `hasSpawned = true` 一起复制过去，随后 `NetworkIdentity.Awake()` 会立刻销毁这个副本，报 *"has already spawned. Don't call Instantiate for NetworkIdentities that were in the scene"*。代码里保留了"克隆一个 inactive 对象"的保底方案，以防找不到 prefab。
3. **生成**：实例化 prefab，用 `NetworkServer.Spawn` 注册 `NetworkIdentity`，并按 `GameStamp` 的做法写入 `GameBase.NetworkcasinoLevel`。
4. **替换交互**：把机器的 `InteractableEventTrigger.serverOnInteractEvent` 换成一个新的空 UnityEvent。原来那个序列化在里面的、调用 `GameBase.TryStartGame` 的监听器被丢弃，因此不会扣注，`StartGame` / `Payout` 都不会执行。
5. **到账**：新的监听器调用 `MoneyManager.TryChangeBalance(reward, profile, ChangeType.GameResult)`。

涉及的原始类型：

| 类型 | 作用 |
| --- | --- |
| `Slots : GameBase : NetworkBehaviour` | 老虎机本体 |
| `GameBase.TryStartGame(PlayerInteract)` | 被绕开的原版按钮逻辑 |
| `GameStamp.gamePrefab` / `GameStamp.SpawnGame` | 游戏自己的机器生成路径 |
| `InteractableEventTrigger` | 持有被替换的 `serverOnInteractEvent` |
| `MoneyManager.TryChangeBalance` | 发放余额的入口 |
| `CasinoFloor.floorIndex` | 写入 `casinoLevel` 的楼层编号 |

## 声明

GambleUp 是非官方的第三方修改，与 TENSTACK 无关，未获其认可或支持。

请在单人中使用；联机时只在你已经明确征得同意的房间里使用。修改自己未拥有的游戏可能违反你购买平台的条款，在公开或竞技房间里使用很容易被移出房间。这个风险由你自己承担。

仓库**不包含**任何游戏素材或反编译的游戏源码。这些文件仅在本地开发时用于分析，已在 `.gitignore` 中排除。
