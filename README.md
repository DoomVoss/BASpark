<div align="center">

![BASpark](https://socialify.git.ci/DoomVoss/BASpark/image?description=1&descriptionEditable=Blue%20Archive%20Style%20Particle%20Effect&forks=1&issues=1&logo=https%3A%2F%2Fraw.githubusercontent.com%2FDoomVoss%2FBASpark%2Fmain%2Fassets%2Flogo.png&name=1&pattern=Diagonal%20Stripes&pulls=1&stargazers=1&theme=Auto)

[![GitHub stars](https://img.shields.io/github/stars/DoomVoss/BASpark?style=social)](https://github.com/DoomVoss/BASpark/stargazers)
[![GitHub license](https://img.shields.io/github/license/DoomVoss/BASpark)](https://github.com/DoomVoss/BASpark/blob/main/LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/DoomVoss/BASpark)](https://github.com/DoomVoss/BASpark/issues)
[![Discord](https://img.shields.io/badge/Discord-Join-7289DA)](https://discord.gg/sgZMdqhfZu)
[![QQ](https://img.shields.io/badge/QQ-Doom-blue)](https://qm.qq.com/q/oGwB5mKQtq)

🌐 **Languages:** English | [简体中文](./docs/README_zh.md)

[Download](https://github.com/DoomVoss/BASpark/releases/latest) | [Bug Report](https://github.com/DoomVoss/BASpark/issues/new?template=en_bug_report.yml) | [Feature Request](https://github.com/DoomVoss/BASpark/issues/new?template=en_feature_request.yml)

</div>

---

# BASpark

> **A Windows Desktop Particle Effect Tool:** Reconstructing the iconic Blue Archive UI interactive visual dynamics using HTML5/Canvas.

**BASpark** is a lightweight, versatile Windows desktop interactive visual effect utility dedicated to precisely reproducing the clicking and particle fluid dynamics seen in the game *Blue Archive*.

---

## Features

BASpark operates on a hybrid rendering architecture utilizing a **WPF host infrastructure powered by localized WebView2 rendering runtimes**.

* **Authentic Visual Fidelity:** Flawlessly replicates the particle responses, easing animations, and interactive textures of the classic Blue Archive visual framework.
* **Aggressive Resource Optimization:** Leveraging WebView2 lifecycle management, rendering procedures are invoked strictly upon input triggers. The graphics context completely hibernates when idle, causing zero overhead to active background processes or system resources.
* **System-Wide Environment Perception:** Full compatibility with full-screen entertainment and production environments. Real-time hook intercepting accurately routes input feedback without interrupting target processes.

---

## Getting Started

### System Specifications
* **Architecture:** Requires a 64-bit processor and native OS environment.
* **Operating System:** Windows 10 / Windows 11 (X64 architectures).
* **Memory:** Minimum 200 MB RAM allocation.
* **Graphics Unit:** Discrete or integrated graphics processing units natively supporting DirectX 11 or OpenGL runtimes.
* **Storage Matrix:** Minimum 200 MB available physical storage workspace.

### Installation
1. Navigate to the official [Releases Hub](https://github.com/DoomVoss/BASpark/releases). Download the latest setup binary: `BASpark_Installer_vX.X.X_x64.exe`.
2. Execute the installer application and follow the deployment wizard instructions to initialize the localized directory.
3. Launch the primary execution routine and enjoy the interactive effects.

---

## Contribution Guide

We encourage open-source community contributions. To set up your local workspace for BASpark engineering:

### Environment Setup
Clone the remote repository locally:

```bash
git clone https://github.com/DoomVoss/BASpark.git
cd BASpark
```

Open the project folder using Visual Studio Code (Ensure the C# Dev Kit extension suite is installed):

```bash
code .
```

* **Live Frontend Engineering:** Because the central animation loops and visual textures are structurally compiled inside HTML5/Canvas components, you can dynamically view and troubleshoot elements in src/web/ in real time via VS Code without rebuilding the whole C# runtime wrapper structure.
* When submitting an issue solution or optimization, open a formal Pull Request (https://github.com/DoomVoss/BASpark/pulls). Ensure your implementation aligns with the project’s established formatting styles and code quality rules.

---

## Disclaimers Clarifications

* This utility is published strictly as a non-commercial, fan-made interactive tribute project. Commercial distribution or unauthorized reselling of this code or asset package is strictly prohibited.
* The source codebase is audited and contains no malicious modules, spyware, telemetry packages, or destructive code. Its functional scope is limited entirely to desktop aesthetic rendering.
* This software distribution is delivered strictly on an "as-is" basis. The maintenance team disclaims any liability for directly or indirectly declared system anomalies or operational losses stemming from execution.
* The fundamental design languages, trademark graphics, and overall creative aesthetics draw artistic inspiration from the intellectual property of Nexon / Yostar (Blue Archive). All related copyrights belong unconditionally to their respective commercial legal owners.

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

## License

This software utility is distributed globally under the terms of the MIT License. For deep structural parameters and legal terminology, refer explicitly to the [LICENSE](./LICENSE) document.

> **MIT License**
>
> Copyright (c) 2026 Doom

<div align="center">
  Made with ❤️ by Doom
</div>