<div align="center">

![BASpark](https://socialify.git.ci/DoomVoss/BASpark/image?description=1&descriptionEditable=Blue%20Archive%20Style%20Particle%20Effect&forks=1&issues=1&logo=https%3A%2F%2Fraw.githubusercontent.com%2FDoomVoss%2FBASpark%2Fmain%2Fassets%2Flogo.png&name=1&pattern=Diagonal%20Stripes&pulls=1&stargazers=1&theme=Auto)

[![GitHub stars](https://img.shields.io/github/stars/DoomVoss/BASpark?style=social)](https://github.com/DoomVoss/BASpark/stargazers)
[![GitHub license](https://img.shields.io/github/license/DoomVoss/BASpark)](https://github.com/DoomVoss/BASpark/blob/main/LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/DoomVoss/BASpark)](https://github.com/DoomVoss/BASpark/issues)
[![QQ](https://img.shields.io/badge/QQ-Doom-blue)](https://qm.qq.com/q/oGwB5mKQtq)

[快速下载](https://github.com/DoomVoss/BASpark/releases/latest) | [反馈 Bug](https://github.com/DoomVoss/BASpark/issues/new?template=bug_report.yml) | [功能建议](https://github.com/DoomVoss/BASpark/issues/new?template=feature_request.yml)

</div>

---
# BASpark

### 项目简介

> Windows 鼠标特效工具：基于 HTML5/Canvas 深度复刻《蔚蓝档案》UI 风格动效
> A Windows mouse effect tool reconstructing Blue Archive UI style using HTML5/Canvas.

**BASpark** 是一款轻量化多功能的桌面点击特效工具，深度复刻游戏 **《蔚蓝档案》(Blue Archive)** 的点击动效。

### 核心特性

BASpark 采用 **“WPF 骨架 + WebView2 渲染”** 的混合架构。

* 精准还原《蔚蓝档案》标志性的交互质感。
* 基于 WebView2 优化，特效触发即渲染，闲置即休眠，不抢占额外系统资源。
* 支持全屏应用与游戏环境，实时感知鼠标动作。

### TODO（Not To Do？）

*  已完成 ＜（＾－＾）＞ 应用黑白名单，支持自定义进程过滤，实现全屏游戏或特定软件环境下自动隐藏特效。
* （开发中...）插件化系统，开放粒子脚本接口，允许用户通过修改本地 js/css 文件轻松分享自定义的点击动效。
* （计划中...）交互联动，捕获鼠标点击以外的交互逻辑，如键盘敲击触发粒子喷发或跟随音乐节奏律动。

想要更多？前往 [功能建议](https://github.com/DoomVoss/BASpark/issues/new?template=feature_request.yml) 补充

---

## 快速开始

### 1. 系统需求
* *需要 64 位处理器和操作系统
* 操作系统 : Windows 10 / 11
* 内存: 200 MB RAM
* 显卡: 支持 DirectX 11 / OpenGL 的集成或独立显卡
* 存储空间: 需要 200 MB 可用空间

### 2. 安装步骤
1. 前往 [Releases](https://github.com/DoomVoss/BASpark/releases) 页面下载最新的安装包 BASpark_Installer_vX.X.X_x64.exe。
2. 运行安装程序并完成安装。
3. Enjoy it！

---

## 开发与贡献

如果你想参与 BASpark 的开发，可以参考以下步骤：

### 克隆仓库

```bash
git clone https://github.com/DoomVoss/BASpark.git
```

### 进入目录

```bash
cd BASpark
```

### 使用 VS Code 打开项目 (需安装 C# Dev Kit 扩展)

```bash
code .
```

* 由于动效逻辑由 HTML5/Canvas 实现，你可以直接在 VS Code 中实时预览和调试 src/web/ 下的代码，而无需频繁编译整个项目。
* 提交 [Pull Request](https://github.com/DoomVoss/BASpark/pulls)，并确保代码风格与现有一致

---

## 免责声明

* 本软件为同人爱好交流项目，严禁任何形式的倒卖行为。
* 本程序不含任何病毒或恶意代码，仅用于桌面视觉特效。
* 软件按“原样”提供，作者不对使用过程中可能产生的任何直接或间接损失承担责任。
* 视觉风格灵感来源于 Nexon / Yostar 《Blue Archive》，版权归原作者所有。

---
## Star History

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=DoomVoss/BASpark&type=Date&theme=dark" />
  <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=DoomVoss/BASpark&type=Date" />
  <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=DoomVoss/BASpark&type=Date" />
</picture>

---

## Contributors

<a href="https://github.com/DoomVoss/BASpark/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=DoomVoss/BASpark&v=0410" />
</a>

---

## 许可协议

- 本项目采用 MIT 许可证，详情请参见 [LICENSE](./LICENSE) 文件

> **MIT License**
>
> Copyright (c) 2026 Doom


<div align="center">
  Made with ❤️ by Doom
</div>