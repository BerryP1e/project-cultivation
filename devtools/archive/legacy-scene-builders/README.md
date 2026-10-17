# 已退役的场景构建器

2026-10-15 五场景迁入统一工坊后归档。文件和原 `.meta` 一起保留在 Assets 外，不参与 Unity 编译，不再注册菜单。

- VillageBuilder、VillageBuilder2、VillageBuilder3：旧村庄摆位／贴地的三个版本。
- VillageNatureBuilder：旧村庄地形植被生成。
- VillageSceneBuilder：旧村庄场景复制与一次性装配，依赖 VillageBuilder。
- SectWildernessBuilder：旧野外地形与植被整图重建。
- SectGroundBaker：旧宗门独立石台烘焙；2026-10-16 随宗门主场景退役。
- FinalCleanup：旧阶段清理清单，含已过时路径。

归档前检查当前 Assets 源码，无外部执行类型调用。当前地图生成、编辑、应用和检查统一由 `Assets/Editor/Tools/WorldSceneMigration.cs`、`WorldWorkshopWindow.cs` 和共享 WorldAuthoring 组件维护。不要直接把旧脚本移回 Assets 后运行，以免覆盖当前场景；若需研究历史算法，可在此只读参考。
