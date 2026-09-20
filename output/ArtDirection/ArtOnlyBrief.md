# ToxicBarrier 美术任务交接（2026-09-19）

## 用户要求

行动前先描述准备做成什么样子，再行动。专门开一个只负责美术的聊天，制作尚未定稿的石料、凝胶、尖牙和塔的新图。塔主要根据已采用的背景来设计。Main Menu UI 后续需要重新设计，但必须等这四类素材定稿，目前不做。

## 参考优先级

1. 当前正式地图：`C:/GameProjects/ToxicBarrier/Assets/Sprites/Sprites_map_1.png`、`Sprites_map_2.png`、`Sprites_map_3.png`。这是用户在另一任务中选定并按顺序替换的三张牙齿遗迹地图，已处理真实 Alpha。第一关旧金/褐石/象牙纹样，第二关冷青绿侵蚀/深红植被，第三关暗金/炭灰/熔裂。必须打开原图看细节。
2. 当前角色、敌人、环境建筑、木头和游戏 HUD。引用清单为本目录 `CurrentAssetReferences.json`，总览为 `CurrentAssetReview.jpg`。清单来自当前场景与预制体的实际 Sprite 引用，图集已提取对应帧；并非将所有导入素材包视作定稿素材。地图缩略图和敌人帧需要进一步放大检查。
3. 黑骑士：`Assets/PeterRadiant/2D Pixel Art Black Knight/Sprites/Black_Knight_All_Frame_With_Border.png`，Player.prefab 实际帧 fileID 776787164；敌人使用 `Assets/Monsters Creatures Fantasy/Sprites/` 下的 Goblin、Flying eye、Skeleton。辅助建筑来自 Tiny Swords 的 Black Buildings（Archery、Castle、House1、House3）。
4. 木头已定，不修改。实际世界掉落使用 `Assets/Resources/Presentation/WoodPickup.asset`，对应木头原图 `Assets/PeterRadiant/Tiny Swords/UI Elements/Icons/Icon_02.png`，HUD 也使用该图。不要误把旧 `Sprites_wood.png` 当作实际使用版本。
5. 现有石料、凝胶、尖牙、三个塔原图只是替换对象，不能作为已定稿美术的主导参考。用户最新明确允许重新设计塔，覆盖此前保留原三套塔的阶段性偏好。

## 应排除的草稿

`output/MapArt/` 中的林地、冷青古道、月蓝石酒红植被是之前的生成草稿，不是当前地图。不要回到早期黄草地，也不要使用被明确否决的红砖 + 金边花砖 + 亮青草组合。不要依赖旧实机截图中的黄色地面判断当前背景风格。

## 本轮交付

- 石料、凝胶、尖牙：每类独立新 PNG；小尺寸下材质和轮廓可辨认，与已有木头同属一套掉落物，不改变资源类别或玩法。
- 塔：设计同一建筑逐级发展的 1/2/3 级，共三张图；石材、金属、齿骨主题来自正式背景。轮廓和结构体现升级，统一视角、底部落点与光向。不要仅靠换色区分等级，不把背景贴在塔上。
- 先给具体美术方向并说明材质/配色/形状，再生成；展示与当前地图、角色和木头对照的预览，供用户定稿。没有用户定稿，不把候选图宣称为已采用。
- 保存 PNG、预览、生成提示词与素材清单。检查真实 Alpha、边缘、缺失像素及缩小后的识别度。生成器曾反复画出假棋盘底，不能凭预览声称透明。
- 只做美术文件和美术相关说明；不修改游戏代码、玩法、数值、场景、Prefab、引用、UI 布局，不自动替换已经使用的资源。

## 协作与工作区

正式项目存在未提交地图、场景与塔特效修改。新任务工作树可能只有旧版素材，因此以此文中的 `C:/GameProjects/ToxicBarrier/` 绝对路径作为只读参考，不能拿工作树旧地图做设计。产物保存在新任务自己的 `output/ArtOnly/`，保留所有已有项目修改，不重置、不提交或推送原工作区。

Main Menu 重设计只登记为下一阶段，等四类资产定稿后再开始，未来仍应保留英文与中文支持。
