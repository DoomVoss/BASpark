(function ()
{
    'use strict';

    const POINTER_ID = 1;
    const DEFAULT_COLOR = '#2dafff';
    const DEFAULT_INPUT_SAMPLING_RATE = 40;
    const EFFECT_HOST_ID = 'baspark-fx-host';
    const HOST_GENERATION =
        typeof window.__basparkRendererGeneration === 'string'
            ? window.__basparkRendererGeneration
            : '';
    const DEFAULT_SETTINGS = Object.freeze(
        {
            scale: 1.5,
            opacity: 1,
            trailSpeed: 1,
            clickSpeed: 1,
        });
    const DOM_CONTENT_LOADED_OPTIONS =
    {
        once: true,
    };
    const state =
    {
        fx: null,
        outputHost: null,
        initialized: false,
        paused: false,
        inputMode: 'mouse',
        alwaysTrailEnabled: false,
        effectiveAlwaysTrail: false,
        activePointerKind: null,
        color: DEFAULT_COLOR,
        inputSamplingRate: DEFAULT_INPUT_SAMPLING_RATE,
        settings:
        {
            ...DEFAULT_SETTINGS,
        },
        lastBoomX: -1,
        lastBoomY: -1,
        lastBoomTime: 0,
        lastMoveX: -1,
        lastMoveY: -1,
    };

    function clamp(value, minimum, maximum)
    {
        return Math.max(minimum, Math.min(maximum, value));
    }

    function errorMessage(error)
    {
        if (error instanceof Error)
        {
            return error.message;
        }

        return String(error);
    }

    function postHostMessage(type, detail = null)
    {
        const message =
        {
            source: 'baspark-fx',
            generation: HOST_GENERATION,
            type,
            ...(detail || Object.create(null)),
        };

        try
        {
            if (
                window.chrome &&
                window.chrome.webview &&
                typeof window.chrome.webview.postMessage === 'function'
            )
            {
                // 宿主统一按 JSON 字符串解析，避免 WebView2 对象封送行为随版本变化。
                window.chrome.webview.postMessage(JSON.stringify(message));
            }
        }
        catch (error)
        {
            console.error('[BASpark FX] 无法向宿主发送消息:', error);
        }
    }

    function reportError(phase, error)
    {
        const message = errorMessage(error);

        console.error(`[BASpark FX] ${phase}:`, error);
        postHostMessage(
            'error',
            {
                phase,
                message,
            });
    }

    function invokeFx(phase, action)
    {
        try
        {
            return action();
        }
        catch (error)
        {
            reportError(phase, error);
            return false;
        }
    }

    function resetInputCache()
    {
        state.activePointerKind = null;
        state.lastBoomX = -1;
        state.lastBoomY = -1;
        state.lastBoomTime = 0;
        state.lastMoveX = -1;
        state.lastMoveY = -1;
    }

    function pointerType()
    {
        return state.inputMode === 'touch' ? 'touch' : 'mouse';
    }

    function toCanvasPoint(percentX, percentY)
    {
        if (!state.fx)
        {
            return null;
        }

        const normalizedX = Number(percentX);
        const normalizedY = Number(percentY);

        if (!Number.isFinite(normalizedX) || !Number.isFinite(normalizedY))
        {
            return null;
        }

        // C# 传入窗口内归一化坐标；公开 API 要求 Canvas 局部 CSS 像素。
        return (
            {
                x: clamp(normalizedX, 0, 1) * state.fx.width,
                y: clamp(normalizedY, 0, 1) * state.fx.height,
                pointerId: POINTER_ID,
                pointerType: pointerType(),
            });
    }

    function cancelPointerImmediately()
    {
        const accepted = state.fx.pointerCancel(POINTER_ID);

        // 宿主取消表示输入所有权切换；清除全部拖尾，避免上一所有者的残留。
        state.fx.clearTrail();
        state.activePointerKind = null;
        return accepted;
    }

    function cancelActivePointer()
    {
        if (!state.fx || state.activePointerKind === null)
        {
            return false;
        }

        return cancelPointerImmediately();
    }

    function applyInputContext()
    {
        if (!state.fx)
        {
            return;
        }

        state.fx.updateConfig(
            {
                trailAlways: state.effectiveAlwaysTrail,
            });
    }

    function applyColor()
    {
        if (!state.fx)
        {
            return;
        }

        state.fx.setThemeColor(state.color);
    }

    function applyOutputOpacity()
    {
        if (!state.outputHost)
        {
            return;
        }

        // 全局透明度必须在 HDR 与 Bloom 完成后衰减，否则高能拖尾在低值下
        // 仍会越过 Bloom 阈值，导致用户几乎看不出透明度变化。
        state.outputHost.style.opacity = String(state.settings.opacity);
    }

    function applyEffectSettings()
    {
        applyOutputOpacity();

        if (!state.fx)
        {
            return;
        }

        state.fx.updateConfig(
            {
                // 旧引擎以 1.5 为默认尺寸，新引擎以 1 为默认尺寸。
                scale: Math.max(0.01, state.settings.scale / 1.5),
                // 用户设置由最外层宿主统一执行，库内保持完整 HDR 发射。
                opacity: 1,
                trailTimeScale: state.settings.trailSpeed,
                clickTimeScale: state.settings.clickSpeed,
            });
    }

    function parseRgbColor(rgbString)
    {
        const channels = String(rgbString).split(',').map((channel) =>
        {
            return Number(channel.trim());
        });

        if (
            channels.length !== 3 ||
            channels.some((channel) => !Number.isFinite(channel))
        )
        {
            return null;
        }

        return `#${channels.map((channel) =>
        {
            return Math.round(clamp(channel, 0, 255))
                .toString(16)
                .padStart(2, '0');
        }).join('')}`;
    }

    function normalizeSpeed(value, fallback)
    {
        const numeric = Number(value);

        if (!Number.isFinite(numeric))
        {
            return fallback;
        }

        return clamp(numeric, 0.2, 3);
    }

    function parseInputSamplingRate(value)
    {
        if (typeof value !== 'number')
        {
            return null;
        }

        const numeric = value;

        if (
            numeric !== 0 &&
            (
                !Number.isFinite(numeric) ||
                numeric < 1 ||
                numeric > 1000
            )
        )
        {
            return null;
        }

        return numeric;
    }

    window.externalBoom = function (percentX, percentY)
    {
        if (state.paused || !state.fx)
        {
            return false;
        }

        const numericX = Number(percentX);
        const numericY = Number(percentY);
        const now = Date.now();

        if (
            numericX === state.lastBoomX &&
            numericY === state.lastBoomY &&
            now - state.lastBoomTime < 25
        )
        {
            return false;
        }

        const point = toCanvasPoint(numericX, numericY);

        if (!point)
        {
            return false;
        }

        state.lastBoomX = numericX;
        state.lastBoomY = numericY;
        state.lastBoomTime = now;

        return invokeFx('externalBoom', function ()
        {
            // 丢失抬起事件时先恢复指针状态，避免后续点击被单指针上限永久拒绝。
            cancelActivePointer();
            const accepted = state.fx.pointerDown(point);

            if (accepted)
            {
                state.activePointerKind = 'press';
            }

            return accepted;
        });
    };

    window.externalMove = function (percentX, percentY)
    {
        if (state.paused || !state.fx)
        {
            return false;
        }

        const numericX = Number(percentX);
        const numericY = Number(percentY);

        if (
            numericX === state.lastMoveX &&
            numericY === state.lastMoveY
        )
        {
            return false;
        }

        const point = toCanvasPoint(numericX, numericY);

        if (!point)
        {
            return false;
        }

        state.lastMoveX = numericX;
        state.lastMoveY = numericY;

        return invokeFx('externalMove', function ()
        {
            const accepted = state.fx.pointerMove(point);

            if (
                accepted &&
                state.activePointerKind === null &&
                state.effectiveAlwaysTrail
            )
            {
                state.activePointerKind = 'hover';
            }

            return accepted;
        });
    };

    window.externalUp = function ()
    {
        if (state.paused || !state.fx)
        {
            return false;
        }

        return invokeFx('externalUp', function ()
        {
            const accepted = state.fx.pointerUp(POINTER_ID);

            if (accepted)
            {
                state.activePointerKind = null;
            }

            return accepted;
        });
    };

    window.externalCancel = function ()
    {
        if (!state.fx)
        {
            resetInputCache();
            return false;
        }

        return invokeFx('externalCancel', function ()
        {
            const accepted = cancelPointerImmediately();

            resetInputCache();
            return accepted;
        });
    };

    window.setRenderingPaused = function (paused)
    {
        state.paused = Boolean(paused);

        if (!state.fx)
        {
            return;
        }

        invokeFx('setRenderingPaused', function ()
        {
            if (state.paused)
            {
                resetInputCache();
                state.fx.setPaused(
                    true,
                    {
                        clear: true,
                    });
                return true;
            }

            state.fx.setPaused(false);
            return true;
        });
    };

    window.setInputContext = function (mode, alwaysTrailEnabled)
    {
        const nextMode = mode === 'touch' ? 'touch' : 'mouse';
        const nextAlwaysTrailEnabled = Boolean(alwaysTrailEnabled);
        const nextEffectiveAlwaysTrail =
            nextMode === 'mouse' && nextAlwaysTrailEnabled;

        if (
            nextMode !== state.inputMode ||
            (
                !nextEffectiveAlwaysTrail &&
                state.activePointerKind === 'hover'
            )
        )
        {
            invokeFx('setInputContext.cancel', function ()
            {
                return cancelActivePointer();
            });
        }

        state.inputMode = nextMode;
        state.alwaysTrailEnabled = nextAlwaysTrailEnabled;
        state.effectiveAlwaysTrail = nextEffectiveAlwaysTrail;

        invokeFx('setInputContext', function ()
        {
            applyInputContext();
            return true;
        });
    };

    window.updateColor = function (rgbString)
    {
        const color = parseRgbColor(rgbString);

        if (!color)
        {
            // 配置损坏不代表渲染器失效，保留当前颜色即可继续运行。
            console.warn('[BASpark FX] 忽略非法 RGB 颜色:', rgbString);
            return false;
        }

        state.color = color;

        return invokeFx('updateColor', function ()
        {
            applyColor();
            return true;
        });
    };

    window.updateInputSamplingRate = function (rateHz)
    {
        const rate = parseInputSamplingRate(rateHz);

        if (rate === null)
        {
            console.warn('[BASpark FX] 忽略非法输入采样率:', rateHz);
            return false;
        }

        state.inputSamplingRate = rate;

        if (!state.fx)
        {
            return true;
        }

        return invokeFx('updateInputSamplingRate', function ()
        {
            return state.fx.setInputSamplingRate(rate);
        });
    };

    window.updateEffectSettings = function (
        scale,
        opacity,
        trailSpeed,
        clickSpeed
    )
    {
        const numericScale = Number(scale);
        const numericOpacity = Number(opacity);
        const safeTrailSpeed = normalizeSpeed(
            trailSpeed,
            DEFAULT_SETTINGS.trailSpeed,
        );

        state.settings =
        {
            scale: Number.isFinite(numericScale) && numericScale > 0
                ? numericScale
                : DEFAULT_SETTINGS.scale,
            opacity: Number.isFinite(numericOpacity)
                ? clamp(numericOpacity, 0.1, 1)
                : DEFAULT_SETTINGS.opacity,
            trailSpeed: safeTrailSpeed,
            clickSpeed: normalizeSpeed(clickSpeed, safeTrailSpeed),
        };

        return invokeFx('updateEffectSettings', function ()
        {
            applyEffectSettings();
            return true;
        });
    };

    function readBackendState(detail = Object.create(null))
    {
        const config = state.fx.getConfig();
        const requestedEffectBackend =
            detail.requestedEffectBackend || config.effectBackend;
        const resolvedEffectBackend =
            detail.resolvedEffectBackend || config.resolvedEffectBackend;
        const requestedBloomBackend =
            detail.requestedBloomBackend || config.bloomBackend;
        const resolvedBloomBackend =
            detail.resolvedBloomBackend || config.resolvedBloomBackend;
        const requestedHostCompositing =
            detail.requestedHostCompositing ||
            config.hostCompositing ||
            'unknown';
        const resolvedHostCompositing =
            detail.resolvedHostCompositing ||
            config.resolvedHostCompositing ||
            config.hostCompositing ||
            'unknown';
        const hostCompositingSurface =
            detail.hostCompositingSurface ||
            config.hostCompositingSurface ||
            'unknown';
        const compositingWarning =
            detail.compositingWarning ?? config.compositingWarning ?? null;

        return (
            {
                backend: resolvedEffectBackend === 'webgl2'
                    ? resolvedEffectBackend
                    : resolvedBloomBackend,
                requestedEffectBackend,
                resolvedEffectBackend,
                requestedBloomBackend,
                resolvedBloomBackend,
                requestedHostCompositing,
                resolvedHostCompositing,
                hostCompositingSurface,
                compositingWarning,
            });
    }

    function handleBackendChange(event)
    {
        const backendState = readBackendState(
            event.detail || Object.create(null),
        );

        postHostMessage(
            'backend',
            backendState,
        );

        if (
            backendState.resolvedEffectBackend !== 'webgl2' &&
            backendState.resolvedBloomBackend === 'software'
        )
        {
            // 全屏 Float32 软件 Bloom 对桌面覆盖层代价过高，GPU 不可用时改用原生辉光。
            state.fx.updateConfig(
                {
                    bloomBackend: 'native',
                });
        }
    }

    function initialize()
    {
        if (state.initialized)
        {
            return;
        }

        state.initialized = true;

        try
        {
            if (
                !window.BAClickFX ||
                typeof window.BAClickFX.BAClickFX !== 'function'
            )
            {
                throw new Error('ba-click-fx IIFE 未在适配器之前注入');
            }

            const outputHost = document.getElementById(EFFECT_HOST_ID);

            if (!outputHost)
            {
                throw new Error('BASpark 特效宿主元素不存在');
            }

            state.outputHost = outputHost;

            state.fx = new window.BAClickFX.BAClickFX(
                {
                    target: outputHost,
                    inputSource: 'manual',
                    opacity: 1,
                    // WebView2 无法读取窗口后的桌面背景；使用 source-over，
                    // 再以浅色背景补偿和 Alpha 上限提高未知背景上的可见性。
                    effectBackend: 'webgl2',
                    bloomBackend: 'webgl2',
                    inputSamplingRate: state.inputSamplingRate,
                    outputCompositing: 'browser-overlay',
                    overlayAlphaPolicy: 'visual-max',
                    overlayColorCompensation: 'bright-core',
                    overlayAlphaLimit: 0.85,
                    hostCompositing: 'source-over',
                    hostCompositingSurface: 'transparent-window',
                    // 公共库为兼容旧像素默认 hue-only；BASpark 的取色器
                    // 明确使用上游推荐的相对 OKLCH 完整颜色映射。
                    themeColorMode: 'relative-oklch',
                    isolatedCompositing: false,
                    lightBackgroundContrastAlpha: 0,
                    maxDpr: 2,
                });

            const bloomBackendEventName =
                window.BAClickFX.BLOOM_BACKEND_CHANGE_EVENT ||
                'baclickfxbackendchange';
            const effectBackendEventName =
                window.BAClickFX.EFFECT_BACKEND_CHANGE_EVENT ||
                'baclickfxeffectbackendchange';
            const hostCompositingEventName =
                window.BAClickFX.HOST_COMPOSITING_CHANGE_EVENT ||
                'baclickfxhostcompositingchange';

            state.fx.canvas.addEventListener(
                bloomBackendEventName,
                handleBackendChange,
            );
            state.fx.canvas.addEventListener(
                effectBackendEventName,
                handleBackendChange,
            );
            state.fx.canvas.addEventListener(
                hostCompositingEventName,
                handleBackendChange,
            );

            applyInputContext();
            applyColor();
            applyEffectSettings();

            if (state.paused)
            {
                state.fx.setPaused(
                    true,
                    {
                        clear: true,
                    });
            }

            postHostMessage(
                'ready',
                readBackendState(),
            );
        }
        catch (error)
        {
            reportError('initialize', error);
        }
    }

    function installCompatibilityShims()
    {
        if (typeof Array.prototype.at !== 'function')
        {
            Object.defineProperty(
                Array.prototype,
                'at',
                {
                    configurable: true,
                    writable: true,
                    value: function (index)
                    {
                        const length = this.length >>> 0;
                        const integer = Math.trunc(Number(index) || 0);
                        const offset = integer < 0 ? length + integer : integer;

                        if (offset < 0 || offset >= length)
                        {
                            return undefined;
                        }

                        return this[offset];
                    },
                });
        }

        if (typeof window.structuredClone !== 'function')
        {
            // 上游只克隆由数字、布尔值、数组和普通对象组成的配置快照。
            window.structuredClone = function (value)
            {
                return JSON.parse(JSON.stringify(value));
            };
        }
    }

    window.addEventListener('error', function (event)
    {
        reportError('window.error', event.error || event.message);
    });

    window.addEventListener('unhandledrejection', function (event)
    {
        reportError('unhandledrejection', event.reason);
    });

    window.addEventListener('beforeunload', function ()
    {
        if (!state.fx)
        {
            return;
        }

        invokeFx('beforeunload', function ()
        {
            state.fx.destroy();
            state.fx = null;
            resetInputCache();
            return true;
        });
    });

    installCompatibilityShims();

    if (document.readyState === 'loading')
    {
        document.addEventListener(
            'DOMContentLoaded',
            initialize,
            DOM_CONTENT_LOADED_OPTIONS,
        );
    }
    else
    {
        initialize();
    }
})();
