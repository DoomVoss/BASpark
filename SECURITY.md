# Security Policy

## 支持的版本 (Supported Versions)

由于 **BASpark** 处于开发迭代阶段，安全修复将优先适配以下版本：

| 版本 (Version) | 支持状态 (Supported) |
| :------- | :------------------ |
| v1.x.x   | :white_check_mark: (当前维护) |
| v0.x.x   | :warning: (仅限重大漏洞修复) |
| < v0.1.0 | :x: (不再支持) |

---

## 报告漏洞 (Reporting a Vulnerability)

**请不要直接通过公开的 Issue 报告安全漏洞。**

如果你发现了可能导致系统风险、隐私泄露或其他安全隐患的问题，请按照以下流程操作：

1.  **私密报告**：
    通过 GitHub 仓库的 [Security Advisories](https://github.com/DoomVoss/BASpark/security/advisories/new) 页面提交私密漏洞报告。
    
2.  **响应时间**：
    我们会在 **48 小时内** 对报告进行初步评估。如果漏洞被确认，我们会创建一个临时的私有分叉 (Private Fork) 进行协作修复。

3.  **漏洞公开**：
    修复完成后，我们会发布新版本并正式公开安全公告（Security Advisory）。如果你愿意，我们会在公告中对你的发现表示感谢。

---

## 开发者安全声明 (Security Disclaimer)

1.  **内核依赖**：BASpark 依赖于 **Microsoft Edge WebView2**。为了确保最佳安全体验，建议用户保持系统自带的 WebView2 Runtime 为最新版本。
2.  **纯净承诺**：本项目仅用于桌面视觉增强，承诺不包含任何形式的后门、恶意代码或未经授权的数据收集行为。
3.  **免责提示**：软件按“原样”提供，作者不对使用过程中因系统兼容性或第三方环境导致的问题承担法律责任。
