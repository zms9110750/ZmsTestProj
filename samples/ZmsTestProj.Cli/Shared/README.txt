// Shared — 存放被多个同级命令共用的子命令
//
// 例：浏览收藏夹、浏览热门、浏览推荐 都有 download 子命令
// 将 download 的实现放在 Shared/DownloadCommand.cs，各父命令引用即可
