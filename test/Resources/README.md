# test/Resources — 测试资源目录

本目录存放**测试用资源文件**，由 `Directory.Build.props` 自动外引用到**所有测试项目**（`IsTest == true`），并复制到测试输出目录（`bin/<config>/<tfm>/Resources/`），测试代码中可直接用相对路径 `Resources/<文件名>` 读取。

## 放什么

- **网络资源**：测试依赖的远程数据（JSON、图片、响应样本等）下载后放这里，测试改为读本地文件——**避免测试直接访问网络**（网络不可用时测试仍可运行，CI 也更快更稳）。
- 大型 fixture、证书、配置文件等测试输入。

## 约定

- 子目录按功能划分，如 `Resources/items/items.json`。
- 测试中引用：`File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "items", "items.json"))`。
- 资源较大或敏感时注意仓库体积与保密。

## 实现方式

`Directory.Build.props` 中：

```xml
<ItemGroup Condition="'$(IsTest)' == 'true'">
  <Content Include="$(MSBuildThisFileDirectory)test/Resources/**"
           Link="Resources/%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`Link` 让所有测试项目共享同一份资源（不复制进项目源码树），`CopyToOutputDirectory` 保证运行时可用。
