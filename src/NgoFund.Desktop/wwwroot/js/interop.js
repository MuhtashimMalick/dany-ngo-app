// Thin interop layer Blazor calls via IJSRuntime.InvokeVoidAsync. Kept deliberately small — one
// function per distinct motion moment, each wrapped in gsap.matchMedia() so
// prefers-reduced-motion is respected without duplicating that check at every call site.

window.ngoFundMotion = (() => {
  const mm = gsap.matchMedia();
  const spinTweens = new Map();

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

    // Tabs.razor: fade+small-rise the newly active panel in on every tab switch — a "Subtle"
    // page-transition-tier motion (200ms, power1.out) since a tab switch is a much smaller
    // context change than a route navigation, not the 300-400ms animatePageContent uses.
    animateTabPanel(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 6 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.2, ease: 'power1.out', overwrite: true, clearProps: 'transform' }
        );
      });
    },

    // YesNoWithDetail.razor: reveal the conditional detail field when a Yes/No toggle flips to
    // Yes. Height:auto isn't tweenable directly, so this reads the wrapper's natural scrollHeight
    // and tweens toward that pixel value — the standard GSAP accordion-reveal technique — then
    // clearProps hands layout back to the browser so later content changes (the user typing more
    // lines into the textarea) aren't clipped by a stale inline height.
    animateDetailReveal(selector) {
      withReducedMotionGuard((reduceMotion) => {
        const el = document.querySelector(selector);
        if (!el) return;
        if (reduceMotion) {
          gsap.set(el, { autoAlpha: 1 });
          return;
        }
        const targetHeight = el.scrollHeight;
        gsap.set(el, { overflow: 'hidden' });
        gsap.fromTo(
          el,
          { autoAlpha: 0, height: 0 },
          { autoAlpha: 1, height: targetHeight, duration: 0.25, ease: 'power2.out', clearProps: 'height,overflow', overwrite: true }
        );
      });
    },

    // GuarantorsEditor.razor: entrance for a guarantor row just added via "Add Guarantor" (max 2,
    // so this is never a stagger — one row at a time).
    animateGuarantorRowIn(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : -8 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.25, ease: 'power2.out', clearProps: 'transform', overwrite: true }
        );
      });
    },

    // GuarantorsEditor.razor: exit for a row being removed. Returns a Promise so the caller can
    // `await` the tween finishing before actually removing the row from the C# list — Blazor has
    // no separate "removing" render state, so the animation has to run to completion first or it
    // would never be seen.
    animateGuarantorRowOut(selector) {
      return new Promise((resolve) => {
        withReducedMotionGuard((reduceMotion) => {
          const el = document.querySelector(selector);
          if (!el || reduceMotion) {
            resolve();
            return;
          }
          gsap.to(el, { autoAlpha: 0, x: -12, duration: 0.2, ease: 'power1.in', overwrite: true, onComplete: resolve });
        });
      });
    },

    // GuarantorsEditor.razor: the non-blocking guarantor-conflict banner appearing on a row after
    // a save completes — a small settle-in so its arrival reads as a direct consequence of the
    // save, not content that silently changed underneath the user.
    animateWarningBanner(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : -6 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.25, ease: 'power2.out', clearProps: 'transform', overwrite: true }
        );
      });
    },

    // ApplicationWizard.razor: page-to-page transition — a small horizontal slide+fade, direction-
    // aware (Next slides in from the right, Back slides in from the left) so the motion itself
    // communicates progress/regress through the step sequence, matching how the client described
    // Google Forms' own page transitions. Kept to the "Standard" page-transition tier (250-300ms,
    // power2.out) per gsap-core — a step change inside one modal is a smaller context change than
    // a full route navigation, but bigger than a tab switch (animateTabPanel's 200ms).
    animateWizardStepIn(selector, direction) {
      withReducedMotionGuard((reduceMotion) => {
        const fromX = reduceMotion ? 0 : direction === 'back' ? -18 : 18;
        gsap.fromTo(
          selector,
          { autoAlpha: 0, x: fromX },
          { autoAlpha: 1, x: 0, duration: reduceMotion ? 0 : 0.3, ease: 'power2.out', overwrite: true, clearProps: 'transform' }
        );
      });
    },

    // ApplicationWizard.razor's step indicator — the active step dot/segment growing slightly and
    // completed steps' checkmarks settling in as the wizard advances.
    animateWizardStepIndicator(activeSelector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          activeSelector,
          { scale: reduceMotion ? 1 : 0.85 },
          { scale: 1, duration: reduceMotion ? 0 : 0.25, ease: 'back.out(1.6)', overwrite: true, clearProps: 'transform' }
        );
      });
    },

    // DocumentSlotList.razor: a just-uploaded document row settling into a slot's file list, and
    // (separately) the slot itself flashing its "satisfied" check once IsSatisfied flips true —
    // the confirmation moment the client asked for ("upload button" should feel like it registered).
    animateDocRowIn(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : -6 },
          { autoAlpha: 1, y: 0, duration: reduceMotion ? 0 : 0.25, ease: 'power2.out', clearProps: 'transform', overwrite: true }
        );
      });
    },

    animateSlotSatisfied(selector) {
      withReducedMotionGuard((reduceMotion) => {
        if (reduceMotion) return;
        gsap.fromTo(
          selector,
          { scale: 0.7, autoAlpha: 0 },
          { scale: 1, autoAlpha: 1, duration: 0.35, ease: 'back.out(2)', overwrite: true, clearProps: 'transform' }
        );
      });
    },

    // CompletenessChecklist.razor: missing-items list re-render (e.g. after a save shrinks the
    // list) — same stagger-fade language as animateTableRows, kept as its own named entry point
    // since a checklist re-render is a distinct motion moment from a data-table page load even
    // though the tween shape matches.
    animateChecklistItems(selector) {
      withReducedMotionGuard((reduceMotion) => {
        gsap.fromTo(
          selector,
          { autoAlpha: 0, y: reduceMotion ? 0 : 6 },
          {
            autoAlpha: 1,
            y: 0,
            duration: reduceMotion ? 0 : 0.25,
            ease: 'power1.out',
            stagger: reduceMotion ? 0 : { each: 0.03, from: 'start' },
            clearProps: 'transform',
            overwrite: true,
          }
        );
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

    // FundTransactionLedgerTable.razor's Download CSV/PDF buttons — a continuous icon spin for
    // the (possibly multi-second, full-history) export request, keyed by selector so paired
    // start/stop calls from Blazor don't need to hold a JS object reference across the await.
    // Under reduced motion this is a no-op: the button's own disabled + "Downloading..." label
    // swap is still the loading signal, just without the spin.
    //
    // This is a *toggleable* (repeat: -1) animation, unlike every other helper in this file which
    // is fire-and-forget — so it can't share the module-level `mm` via withReducedMotionGuard()
    // above. That shared instance is never reverted, by design: its handlers are meant to live for
    // the app's lifetime and simply replay a finished entrance animation if prefers-reduced-motion
    // changes later, which is harmless. An infinite spin is not harmless to leave registered that
    // way — every start would add another permanent listener on the shared `mm`, and a later
    // prefers-reduced-motion change would re-fire all of them, resurrecting spins whose export
    // already finished. So each spin gets its own scoped gsap.matchMedia() instance, reverted (not
    // just tween.kill()'d) in stopButtonSpin, leaving zero registered listeners once stopped.
    startButtonSpin(selector) {
      const existing = spinTweens.get(selector);
      if (existing) {
        existing.tween?.kill();
        existing.mm.revert();
      }

      const spinMm = gsap.matchMedia();
      const entry = { mm: spinMm, tween: null };
      spinTweens.set(selector, entry);

      spinMm.add({ reduceMotion: '(prefers-reduced-motion: reduce)' }, (context) => {
        const { reduceMotion } = context.conditions;
        if (reduceMotion) return;
        entry.tween = gsap.to(selector, { rotation: 360, duration: 0.8, ease: 'none', repeat: -1 });
      });
    },

    stopButtonSpin(selector) {
      const entry = spinTweens.get(selector);
      if (entry) {
        entry.tween?.kill();
        entry.mm.revert();
        gsap.set(selector, { clearProps: 'rotation' });
        spinTweens.delete(selector);
      }
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

// Tabs.razor: moves focus to the newly active tab button after Left/Right/Home/End navigation
// (roving-tabindex per the WAI-ARIA tabs pattern — the tab that just became active is the only
// one left in the Tab order, so keyboard users need focus moved there explicitly). Split out from
// ngoFundMotion since it's plain DOM focus management, not an animation.
window.ngoFundTabs = {
  focus(id) {
    document.getElementById(id)?.focus();
  },
};

// FormatField.razor's CNIC/mobile masked inputs — forces the real DOM <input> value to match
// the C#-computed masked value on every keystroke. Needed because Blazor's render diff skips
// writing `value` back to the DOM when the newly computed string equals what was already
// rendered (e.g. typing a letter contributes no digits, so the mask output is unchanged) — the
// browser's own <input> already has that stray character sitting in its live DOM value by the
// time the C# handler runs, and without this, Blazor never touches `.value` again to remove it.
window.ngoFundMask = {
  syncValue(el, value) {
    if (!el || el.value === value) return;
    el.value = value;
    const pos = value.length;
    el.setSelectionRange(pos, pos);
  },
};

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
