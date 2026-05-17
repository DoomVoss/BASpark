# Security Policy / 安全政策

## Supported Versions / 支持的版本

As **BASpark** is actively evolving through iterative development, security patches will be prioritized for the following versions:
由于 **BASpark** 处于开发迭代阶段，安全修复将优先适配以下版本：

| Version / 版本 | Supported / 支持状态 |
| :--- | :--- |
| v1.x.x | :white_check_mark: Active Maintenance / 当前维护 |
| v0.x.x | :warning: Critical Fixes Only / 仅限重大漏洞修复 |
| < v0.1.0 | :x: No Longer Supported / 不再支持 |

---

## Reporting a Vulnerability / 报告漏洞

**Please DO NOT report security vulnerabilities via public Issues.**
**请不要直接通过公开的 Issue 报告安全漏洞。**

If you discover any issues that could lead to system risks, privacy leaks, or other security hazards, please follow the procedure below:
如果你发现了可能导致系统风险、隐私泄露或其他安全隐患的问题，请按照以下流程操作：

1. **Private Report / 私密报告**:  
   Submit a private vulnerability report via the GitHub repository's [Security Advisories](https://github.com/DoomVoss/BASpark/security/advisories/new) page.  
   通过 GitHub 仓库的 [Security Advisories](https://github.com/DoomVoss/BASpark/security/advisories/new) 页面提交私密漏洞报告。

2. **Response Timeline / 响应时间**:  
   We will conduct an initial assessment of the report within **48 hours**. If confirmed, a temporary private fork will be created to collaborate on the fix.  
   我们会在 **48 小时内** 对报告进行初步评估。如果漏洞被确认，我们会创建一个临时的私有分叉 (Private Fork) 进行协作修复。

3. **Public Disclosure / 漏洞公开**:  
   Once patched, a new version will be released along with an official Security Advisory. With your permission, we will gladly credit your discovery in the announcement.  
   修复完成后，我们会发布新版本并正式公开安全公告（Security Advisory）。如果你愿意，我们会在公告中对你的发现表示感谢。

---

## Security Disclaimer / 开发者安全声明

1. **Core Dependencies / 内核依赖**:  
   BASpark relies functionally on **Microsoft Edge WebView2**. To ensure an optimal security environment, users are highly recommended to keep their system's WebView2 Runtime updated to the latest version.  
   BASpark 依赖于 **Microsoft Edge WebView2**。为了确保最佳安全体验，建议用户保持系统自带的 WebView2 Runtime 为最新版本。

2. **Integrity Assurance / 纯净承诺**:  
   This project is dedicated solely to desktop visual enhancement. We guarantee that the software contains no backdoors, malicious code, or unauthorized data collection behaviors of any kind.  
   本项目仅用于桌面视觉增强，承诺不包含任何形式的后门、恶意代码或未经授权的数据收集行为。

3. **Liability Limitation / 免责提示**:  
   The software is provided "as-is". The authors assume no legal liability for any operational anomalies or issues arising from system incompatibilities or third-party environments.  
   软件按“原样”提供，作者不对使用过程中因系统兼容性或第三方环境导致的问题承担法律责任。