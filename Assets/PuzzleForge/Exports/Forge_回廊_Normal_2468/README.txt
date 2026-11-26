PuzzleForge 导出说明
1) all_levels.txt / level_XX.txt：ASCII 盘面 + 提示 + 最少步数答案，可直接对照关卡图例。
2) Forge_回廊_Normal_2468_Levels.cs.txt：把内容另存为 .cs（或直接粘进 Campaign）即可开玩。留 .txt 后缀是为了不触发 Unity 重编译。
3) Forge_回廊_Normal_2468.json：完整工程档，可在 PuzzleForge 场景的“读取工程”里回灌继续改。
4) Scenes/：每关一个正式关卡场景，Hierarchy 与 Level_01..Level_21 完全一致
   （GameRoot 上 WaterGame + LevelManager + LevelDefinition，GameCanvas 用 UI_GameHud 预制体，
   GameCanvas/BoardCenter 下挂 Room（LevelBoard），Room 里是 Tile_Wall / Tile_Floor /
   Effect_Water / Effect_Foam / Prop_Exit / Player_Fish 的真实预制体实例），
   打开直接按 Play 就能玩，操作、HUD、提示、旋转、过关演出都走正式关卡那套。
   Play 模式下建不了场景，所以点完按钮后要先退出播放，场景才会出现在 Scenes/ 里。
   也可以随时用菜单 Tools/PuzzleForge/用最近工程生成正式关卡场景 重新生成。
   注意：LevelManager.levelIndex 是包内序号，“下一关”仍按 Campaign 的 Level_XX 走，
   要塞进主线需要把场景改名为 Level_XX 并扩 Campaign/Campaign.Levels。
5) 已排队场景生成，退出播放后自动开跑。
