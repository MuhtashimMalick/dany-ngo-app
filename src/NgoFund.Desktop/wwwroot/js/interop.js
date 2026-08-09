// Thin interop layer Blazor calls via IJSRuntime.InvokeVoidAsync. Kept deliberately small — one
// function per distinct motion moment, each wrapped in gsap.matchMedia() so
// prefers-reduced-motion is respected without duplicating that check at every call site.

window.ngoFundMotion = (() => {
  const mm = gsap.matchMedia();

  function withReducedMotionGuard(runAnimation) {
    mm.add(
      { reduceMotion: '(prefers-reduced-motion: reduce)' },
      (context) => {
        const { reduceMotion } = context.conditions;
        runAnimation(reduceMotion);
      }
    );
  }

  return {
    // Login/auth card entrance — a single settle-in, not decorative.
    animateAuthCard(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 16 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.4, ease: 'power2.out' }
        );
      });
    },

    // App-shell sidebar nav items staggering in once on shell mount.
    animateSidebarNav(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, x: reduceMotion ? 0 : -8 },
          {
            autoAlpha: 1,
            x: 0,
            duration: reduceMotion ? 0 : 0.35,
            ease: 'power2.out',
            stagger: reduceMotion ? 0 : 0.05,
          }
        );
      });
    },

    // Data-table rows staggering in when a page of results loads.
    // clearProps: 'transform' matters beyond tidiness here — any lingering inline transform (even
    // the identity translate(0px,0px) GSAP leaves behind) creates a new stacking context and
    // containing block on the <tr> per the CSS Transforms spec, which traps any position:fixed/
    // absolute overlay anchored inside it (e.g. OverflowMenu's dropdown) and lets later sibling
    // rows paint over it. See the P0 root-cause writeup for the overflow-menu bug this caused.
    animateTableRows(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 8 },
          {
            autoAlpha: 1,
            y: 0,
            duration: reduceMotion ? 0 : 0.3,
            ease: 'power2.out',
            stagger: reduceMotion ? 0 : { each: 0.03, from: 'start' },
            clearProps: 'transform',
          }
        );
      });
    },

    // Modal open — scale+fade settle, matches the "back.out" overshoot the design system calls for.
    // clearProps: 'transform' — same stacking-context issue as animateTableRows above, but for
    // .modal: without it, Combobox's results menu rendered inside the modal body gets trapped in
    // the modal's own stacking context for the rest of its lifetime.
    animateModalIn(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, scale: reduceMotion ? 1 : 0.96 },
          { autoAlpha: 1, scale: 1, duration: reduceMotion ? 0 : 0.3, ease: 'back.out(1.4)', clearProps: 'transform' }
        );
      });
    },

    // Shake feedback for a failed form submission (e.g. invalid login).
    shake(selector) {
      withReducedMotionGuard((reduceMotion) => {
        if (reduceMotion) return;
        gsap.fromTo(
          selector,
          { x: -6 },
          { x: 0, duration: 0.4, ease: 'elastic.out(1, 0.3)' }
        );
      });
    },

    // Dashboard stat cards: entrance stagger with a slight overshoot, plus a count-up tween for
    // any value element carrying data-countup (the target number) and optional
    // data-countup-prefix / data-countup-decimals for formatting. Cards without a data-countup
    // value element (non-numeric stats) just get the entrance.
    animateStatCards(selector) {
      withReducedMotionGuard((reduceMotion) => {
        const cards = gsap.utils.toArray(selector);

        gsap.fromTo(
          cards,
          { autoAlpha: 0, y: reduceMotion ? 0 : 18, scale: reduceMotion ? 1 : 0.95 },
          {
            autoAlpha: 1,
            y: 0,
            scale: 1,
            duration: reduceMotion ? 0 : 0.45,
            ease: 'back.out(1.5)',
            stagger: reduceMotion ? 0 : { each: 0.07, from: 'start' },
            overwrite: true,
            clearProps: 'transform',
          }
        );

        if (reduceMotion) return;

        cards.forEach((card) => {
          const valueEl = card.querySelector('[data-countup]');
          if (!valueEl) return;

          const target = parseFloat(valueEl.getAttribute('data-countup'));
          if (Number.isNaN(target)) return;

          const decimals = parseInt(valueEl.getAttribute('data-countup-decimals') || '0', 10);
          const prefix = valueEl.getAttribute('data-countup-prefix') || '';
          const suffix = valueEl.getAttribute('data-countup-suffix') || '';
          const counter = { value: 0 };

          gsap.to(counter, {
            value: target,
            duration: 1,
            delay: 0.2,
            ease: 'power2.out',
            overwrite: true,
            onUpdate: () => {
              valueEl.textContent = prefix + counter.value.toLocaleString(undefined, {
                minimumFractionDigits: decimals,
                maximumFractionDigits: decimals,
              }) + suffix;
            },
          });
        });
      });
    },

    // Page-content fade on route navigation — called from MainLayout's LocationChanged handler
    // so moving between screens feels like a single continuous app instead of a hard cut.
    // overwrite:true matters here since fast repeated navigation must not queue up tweens.
    animatePageContent(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 10 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.3, ease: 'power2.out', overwrite: true, clearProps: 'transform' }
        );
      });
    },

    // Applicant "view full photo" tile (Applicants.razor detail modal) — hover/focus overlay
    // fade + a slight scale on the tile itself, as a real GSAP tween instead of a CSS :hover
    // transition, so this micro-interaction is consistent with every other motion moment in the
    // app and gets the same prefers-reduced-motion guard for free.
    animatePhotoTileHover(overlayEl, tileEl, isEntering) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.to(overlayEl, {
          autoAlpha: isEntering ? 1 : 0,
          duration: reduceMotion ? 0 : 0.15,
          ease: 'power1.out',
          overwrite: true,
        });
        gsap.to(tileEl, {
          scale: isEntering && !reduceMotion ? 1.04 : 1,
          duration: reduceMotion ? 0 : 0.15,
          ease: 'power1.out',
          overwrite: true,
        });
      });
    },

    // Sidebar collapse toggle — chevron rotation only. The sidebar's width change (and the nav
    // label/brand-text crossfade) is a plain CSS transition driven by MainLayout toggling the
    // `.collapsed` class: it's a simple two-state, always-reversible toggle with no sequencing or
    // JS-computed values, and it has to be a real width change (not a transform) so the flex
    // layout actually reflows `.main-content` — animating `width` with GSAP is explicitly the
    // kind of layout-thrashing tween gsap-core's own guidance says to avoid. The chevron's
    // rotation is the one part of this interaction that's genuinely a transform, so it's the one
    // part GSAP owns. `immediate` skips the tween (snaps instantly) for the initial render, where
    // the icon should just reflect whatever state was restored from localStorage, not animate in.
    animateSidebarToggle(iconSelector, collapsed, immediate) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.to(iconSelector, {
          rotation: collapsed ? -90 : 90,
          duration: reduceMotion || immediate ? 0 : 0.3,
          ease: 'power2.out',
          overwrite: true,
        });
      });
    },
  };
})();

// Tiny localStorage wrapper for simple UI preferences that should survive an app restart (e.g.
// sidebar collapsed state) — WebView2 persists localStorage across launches via its per-app user
// data folder, same as any browser profile. Wrapped in try/catch since localStorage can throw in
// some WebView2 embedding/permission configurations, and losing a UI preference silently is
// better than surfacing an error for something this low-stakes.
window.ngoFundPrefs = {
  get(key) {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },

  set(key, value) {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Best-effort.
    }
  },
};

// ---------- Floating overlay positioning ----------
// Shared by OverflowMenu's row-action menu and Combobox's results list — the fix for both being
// clipped/mispositioned when trapped inside an ancestor's stacking context or an overflow:auto
// scroll container (e.g. .modal-body). Anchoring the panel as `position: fixed`, computed fresh
// from the trigger's own getBoundingClientRect(), escapes both problems regardless of what's
// animating or scrolling between the trigger and the viewport. One helper, two call sites — per
// the design review, duplicating this per-component is exactly what caused the inconsistency
// last time.
window.ngoFundOverlay = (() => {
  const active = new Map(); // panel element -> reposition listener, so `hide` can detach it

  function computeAndApply(trigger, panel, options) {
    const gap = options.gap ?? 4;
    const stretch = !!options.stretch;
    const align = options.align || 'end'; // 'start' | 'end' — ignored when stretch is true

    const triggerRect = trigger.getBoundingClientRect();

    // Reset any previously-applied width before measuring natural size (skipped when stretching,
    // since we're about to force the width to the trigger's anyway).
    if (!stretch) {
      panel.style.width = '';
    }
    const panelRect = panel.getBoundingClientRect();

    const viewportH = document.documentElement.clientHeight;
    const viewportW = document.documentElement.clientWidth;

    // Flip up when there's not enough room below and more room above (spec: "flip-up when near
    // the viewport bottom").
    let top = triggerRect.bottom + gap;
    const spaceBelow = viewportH - triggerRect.bottom;
    const spaceAbove = triggerRect.top;
    if (spaceBelow < panelRect.height + gap && spaceAbove > spaceBelow) {
      top = triggerRect.top - panelRect.height - gap;
    }
    top = Math.max(gap, Math.min(top, viewportH - gap - panelRect.height));

    let left = stretch || align === 'start' ? triggerRect.left : triggerRect.right - panelRect.width;
    left = Math.max(gap, Math.min(left, viewportW - panelRect.width - gap));

    panel.style.position = 'fixed';
    panel.style.top = `${top}px`;
    panel.style.left = `${left}px`;
    panel.style.right = 'auto';
    panel.style.margin = '0';
    if (stretch) {
      panel.style.width = `${triggerRect.width}px`;
    }
  }

  return {
    // trigger/panel: real DOM elements (Blazor ElementReference marshals straight to these).
    show(trigger, panel, options) {
      if (!trigger || !panel) {
        return;
      }

      computeAndApply(trigger, panel, options || {});

      const reposition = () => computeAndApply(trigger, panel, options || {});
      window.addEventListener('resize', reposition);
      window.addEventListener('scroll', reposition, true);
      active.set(panel, reposition);
    },

    hide(panel) {
      const reposition = active.get(panel);
      if (reposition) {
        window.removeEventListener('resize', reposition);
        window.removeEventListener('scroll', reposition, true);
        active.delete(panel);
      }
    },
  };
})();

// ---------- Chart container width measurement ----------
// The fix for the dashboard's "SVG scales 2x, axis text/strokes render mismatched" bug: rather
// than a viewBox that CSS stretches (which scales <text> along with everything else — never
// allowed per the design review), the caller measures its own container in real CSS pixels via
// ResizeObserver and renders the SVG at that exact width/height, so 1 SVG user unit is always
// exactly 1 CSS pixel.
window.ngoFundCharts = (() => {
  const observers = new Map();

  return {
    observeWidth(containerId, dotNetRef) {
      const el = document.getElementById(containerId);
      if (!el) {
        return;
      }

      const notify = () => {
        const width = el.clientWidth;
        if (width > 0) {
          dotNetRef.invokeMethodAsync('OnChartWidthChanged', width);
        }
      };

      const observer = new ResizeObserver(notify);
      observer.observe(el);
      observers.set(containerId, observer);
      notify();
    },

    unobserve(containerId) {
      const observer = observers.get(containerId);
      if (observer) {
        observer.disconnect();
        observers.delete(containerId);
      }
    },
  };
})();

// Shared Modal.razor behavior — Escape-to-close, focus trap, focus restore to the trigger
// element, and body scroll lock. A stack (not a single "active modal") because the app does
// legitimately nest modals (e.g. DocumentViewer opened from inside the Applications manage
// modal): only the top of the stack should react to Escape/Tab, and body-scroll-lock/keydown
// listener should only be torn down once every modal underneath has also closed.
window.ngoFundModal = (() => {
  const stack = [];

  function getFocusable(container) {
    return Array.from(
      container.querySelectorAll(
        'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
      )
    ).filter((el) => el.offsetParent !== null);
  }

  function handleKeydown(e) {
    if (stack.length === 0) return;
    const top = stack[stack.length - 1];

    if (e.key === 'Escape') {
      e.preventDefault();
      top.dotNetRef.invokeMethodAsync('RequestCloseFromJsAsync');
      return;
    }

    if (e.key === 'Tab') {
      const focusable = getFocusable(top.el);
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
  }

  return {
    // id: a caller-supplied string stable for this modal instance's lifetime (its DOM id works
    // well) — used to find the right stack entry on close instead of re-querying a selector that
    // may already be gone from the DOM by the time DisposeAsync runs.
    open(id, selector, dotNetRef) {
      const el = document.querySelector(selector);
      if (!el) return;

      window.ngoFundMotion.animateModalIn(selector);

      if (stack.length === 0) {
        document.body.style.overflow = 'hidden';
        document.addEventListener('keydown', handleKeydown, true);
      }

      stack.push({ id, el, dotNetRef, previouslyFocused: document.activeElement });

      const focusable = getFocusable(el);
      (focusable[0] || el).focus();
    },

    close(id) {
      const index = stack.findIndex((entry) => entry.id === id);
      if (index === -1) return;

      const [entry] = stack.splice(index, 1);
      if (entry.previouslyFocused && document.body.contains(entry.previouslyFocused)) {
        entry.previouslyFocused.focus();
      }

      if (stack.length === 0) {
        document.removeEventListener('keydown', handleKeydown, true);
        document.body.style.overflow = '';
      }
    },
  };
})();

// Toast.razor / ToastService — enter/exit animation only; the queue and auto-dismiss timing are
// plain C# state in ToastService so Blazor stays the source of truth for *what* is showing.
window.ngoFundToast = (() => {
  const mm = gsap.matchMedia();

  return {
    enter(selector) {
      mm.add({ reduceMotion: '(prefers-reduced-motion: reduce)' }, (context) => {
        const { reduceMotion } = context.conditions;
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 12, scale: reduceMotion ? 1 : 0.97 },
          { autoAlpha: 1, y: 0, scale: 1, duration: reduceMotion ? 0 : 0.3, ease: 'power2.out' }
        );
      });
    },
  };
})();

// Webcam capture — one active stream at a time, matching the single WebcamCapture component
// instance a form ever shows. Kept separate from ngoFundMotion since it's a media/device
// concern, not an animation one.
window.ngoFundWebcam = (() => {
  let activeStream = null;

  return {
    async start(videoElementId) {
      const video = document.getElementById(videoElementId);
      activeStream = await navigator.mediaDevices.getUserMedia({ video: { width: 480, height: 360 }, audio: false });
      video.srcObject = activeStream;
      await video.play();
    },

    capture(videoElementId, canvasElementId) {
      const video = document.getElementById(videoElementId);
      const canvas = document.getElementById(canvasElementId);
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
      return canvas.toDataURL('image/jpeg', 0.9);
    },

    stop() {
      if (activeStream) {
        activeStream.getTracks().forEach((track) => track.stop());
        activeStream = null;
      }
    },
  };
})();

// Handles bytes handed back from the API (base64-encoded, since IJSRuntime can't pass a raw
// byte[] as a JS ArrayBuffer without extra interop plumbing) — either as a named-file download,
// or as a blob: URL an <img>/<iframe> can point at for in-app preview.
window.ngoFundFiles = {
  _toBlob(base64, contentType) {
    const byteChars = atob(base64);
    const byteNumbers = new Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) {
      byteNumbers[i] = byteChars.charCodeAt(i);
    }
    return new Blob([new Uint8Array(byteNumbers)], { type: contentType });
  },

  downloadBase64(base64, fileName, contentType) {
    const url = URL.createObjectURL(this._toBlob(base64, contentType));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  },

  createObjectUrl(base64, contentType) {
    return URL.createObjectURL(this._toBlob(base64, contentType));
  },

  revokeObjectUrl(url) {
    URL.revokeObjectURL(url);
  },
};
