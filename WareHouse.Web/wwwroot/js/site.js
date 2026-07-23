(() => {
    "use strict";

    const root = document.documentElement;
    const themeToggle = document.getElementById("themeToggle");
    const fontSizeToggle = document.getElementById("fontSizeToggle");
    const fontSizePanel = document.getElementById("fontSizePanel");
    const fontSizeCurrent = document.getElementById("fontSizeCurrent");
    const themeColorMeta = document.getElementById("themeColorMeta");
    const themeStorageKey = "warehouse.theme";
    const fontSizeStorageKey = "warehouse.fontSize";
    const fontSizes = ["small", "normal", "large"];
    const fontSizeLabels = {
        small: "Nhỏ",
        normal: "Mặc định",
        large: "Lớn"
    };

    const savePreference = (key, value) => {
        try {
            localStorage.setItem(key, value);
        } catch {
            // Storage may be disabled; the preference still applies to the current page.
        }
    };

    const updateThemeControl = () => {
        const dark = root.dataset.theme === "dark";
        const label = dark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối";
        themeToggle?.setAttribute("aria-label", label);
        themeToggle?.setAttribute("title", label);
        themeToggle?.setAttribute("aria-pressed", String(dark));
        if (themeColorMeta) themeColorMeta.content = dark ? "#0b1220" : "#f4f6fb";
    };

    const applyTheme = (theme, persist = true) => {
        const nextTheme = theme === "dark" ? "dark" : "light";
        root.dataset.theme = nextTheme;
        root.style.colorScheme = nextTheme;
        if (persist) savePreference(themeStorageKey, nextTheme);
        updateThemeControl();
    };

    const updateFontSizeControl = () => {
        const current = fontSizes.includes(root.dataset.fontSize)
            ? root.dataset.fontSize
            : "normal";
        if (fontSizeCurrent) fontSizeCurrent.textContent = fontSizeLabels[current];

        const currentIndex = fontSizes.indexOf(current);
        document.querySelectorAll("[data-font-action]").forEach((button) => {
            const action = button.dataset.fontAction;
            button.disabled =
                (action === "decrease" && currentIndex === 0) ||
                (action === "increase" && currentIndex === fontSizes.length - 1);
        });
    };

    const applyFontSize = (fontSize, persist = true) => {
        const nextSize = fontSizes.includes(fontSize) ? fontSize : "normal";
        root.dataset.fontSize = nextSize;
        if (persist) savePreference(fontSizeStorageKey, nextSize);
        updateFontSizeControl();
    };

    const closeFontSizePanel = () => {
        if (!fontSizePanel || !fontSizeToggle) return;
        fontSizePanel.hidden = true;
        fontSizeToggle.setAttribute("aria-expanded", "false");
    };

    themeToggle?.addEventListener("click", () => {
        applyTheme(root.dataset.theme === "dark" ? "light" : "dark");
    });

    fontSizeToggle?.addEventListener("click", () => {
        if (!fontSizePanel) return;
        const willOpen = fontSizePanel.hidden;
        fontSizePanel.hidden = !willOpen;
        fontSizeToggle.setAttribute("aria-expanded", String(willOpen));
    });

    document.querySelectorAll("[data-font-action]").forEach((button) => {
        button.addEventListener("click", () => {
            const current = fontSizes.includes(root.dataset.fontSize)
                ? root.dataset.fontSize
                : "normal";
            const currentIndex = fontSizes.indexOf(current);
            const action = button.dataset.fontAction;
            const nextSize = action === "reset"
                ? "normal"
                : fontSizes[Math.max(0, Math.min(
                    fontSizes.length - 1,
                    currentIndex + (action === "increase" ? 1 : -1)
                ))];
            applyFontSize(nextSize);
        });
    });

    document.addEventListener("click", (event) => {
        if (!fontSizePanel || fontSizePanel.hidden) return;
        if (event.target instanceof Node &&
            !fontSizePanel.contains(event.target) &&
            !fontSizeToggle?.contains(event.target)) {
            closeFontSizePanel();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && fontSizePanel && !fontSizePanel.hidden) {
            closeFontSizePanel();
            fontSizeToggle?.focus();
        }
    });

    applyTheme(root.dataset.theme, false);
    applyFontSize(root.dataset.fontSize, false);
})();

(() => {
    "use strict";

    const body = document.body;
    const sidebar = document.getElementById("appSidebar");
    const collapseButton = document.getElementById("sidebarCollapseButton");
    const mobileButton = document.getElementById("mobileSidebarButton");
    const backdrop = document.getElementById("sidebarBackdrop");
    const desktopQuery = window.matchMedia("(min-width: 992px)");
    const storageKey = "warehouse.sidebar.collapsed";

    if (!sidebar || !collapseButton || !mobileButton || !backdrop) return;

    const storedCollapsed = () => {
        try {
            return localStorage.getItem(storageKey) === "true";
        } catch {
            return false;
        }
    };

    const rememberCollapsed = (collapsed) => {
        try {
            localStorage.setItem(storageKey, collapsed ? "true" : "false");
        } catch {
            // Storage may be disabled; the sidebar still works for the current page.
        }
    };

    const updateCollapseButton = () => {
        const collapsed = body.classList.contains("sidebar-collapsed");
        const label = collapsed ? "Mở rộng menu" : "Thu gọn menu";
        collapseButton.setAttribute("aria-label", label);
        collapseButton.setAttribute("title", label);
    };

    const setDesktopCollapsed = (collapsed, persist = true) => {
        body.classList.toggle("sidebar-collapsed", collapsed);
        if (persist) rememberCollapsed(collapsed);
        updateCollapseButton();
    };

    const openMobileSidebar = () => {
        body.classList.add("sidebar-mobile-open");
        mobileButton.setAttribute("aria-expanded", "true");
        mobileButton.setAttribute("aria-label", "Đóng menu");
    };

    const closeMobileSidebar = () => {
        body.classList.remove("sidebar-mobile-open");
        mobileButton.setAttribute("aria-expanded", "false");
        mobileButton.setAttribute("aria-label", "Mở menu");
    };

    const applyViewportMode = () => {
        closeMobileSidebar();
        if (desktopQuery.matches) {
            setDesktopCollapsed(storedCollapsed(), false);
        } else {
            body.classList.remove("sidebar-collapsed");
            updateCollapseButton();
        }
    };

    collapseButton.addEventListener("click", () => {
        if (desktopQuery.matches) {
            setDesktopCollapsed(!body.classList.contains("sidebar-collapsed"));
        } else {
            closeMobileSidebar();
            mobileButton.focus();
        }
    });

    mobileButton.addEventListener("click", () => {
        if (body.classList.contains("sidebar-mobile-open")) closeMobileSidebar();
        else openMobileSidebar();
    });

    backdrop.addEventListener("click", () => {
        closeMobileSidebar();
        mobileButton.focus();
    });

    sidebar.querySelectorAll("a").forEach((link) => {
        link.addEventListener("click", () => {
            if (!desktopQuery.matches) closeMobileSidebar();
        });
    });

    sidebar.querySelectorAll(".sidebar-group-toggle").forEach((toggle) => {
        toggle.addEventListener("click", (event) => {
            if (!desktopQuery.matches || !body.classList.contains("sidebar-collapsed")) return;

            event.preventDefault();
            event.stopPropagation();
            setDesktopCollapsed(false);

            const targetSelector = toggle.getAttribute("data-bs-target");
            const target = targetSelector ? document.querySelector(targetSelector) : null;
            if (target && window.bootstrap) {
                requestAnimationFrame(() => {
                    bootstrap.Collapse.getOrCreateInstance(target, { toggle: false }).show();
                });
            }
        }, true);
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && body.classList.contains("sidebar-mobile-open")) {
            closeMobileSidebar();
            mobileButton.focus();
        }
    });

    if (typeof desktopQuery.addEventListener === "function") {
        desktopQuery.addEventListener("change", applyViewportMode);
    } else {
        desktopQuery.addListener(applyViewportMode);
    }

    applyViewportMode();
})();

(() => {
    "use strict";

    const digitsOnly = (value) => String(value ?? "").replace(/\D/g, "");
    const formatMoney = (value) => {
        const digits = digitsOnly(value).replace(/^0+(?=\d)/, "");
        return (digits || "0").replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    };

    const syncMoneyInput = (input) => {
        const hidden = input.parentElement?.querySelector(".money-value");
        if (!hidden) return;

        const digits = digitsOnly(input.value).replace(/^0+(?=\d)/, "") || "0";
        input.value = formatMoney(digits);
        hidden.value = digits;
    };

    const bindMoneyInputs = (root = document) => {
        root.querySelectorAll(".money-input").forEach((input) => {
            const hidden = input.parentElement?.querySelector(".money-value");
            input.value = formatMoney(input.value || hidden?.value);
            syncMoneyInput(input);
        });
    };

    bindMoneyInputs();
    document.addEventListener("input", (event) => {
        if (event.target instanceof HTMLInputElement && event.target.matches(".money-input")) {
            syncMoneyInput(event.target);
        }
    }, true);

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (!(node instanceof Element)) return;
                if (node.matches(".money-input")) syncMoneyInput(node);
                bindMoneyInputs(node);
            });
        });
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();

(() => {
    "use strict";

    const jq = window.jQuery;
    const selector = 'select.form-select:not([data-select2="false"])';

    if (!jq?.fn?.select2) return;

    const collect = (root) => {
        const selects = [];
        if (root instanceof HTMLSelectElement && root.matches(selector)) selects.push(root);
        if (root?.querySelectorAll) selects.push(...root.querySelectorAll(selector));
        return selects;
    };

    const renderImageOption = (state) => {
        const imagePath = state.element?.dataset?.image;
        if (!imagePath) return state.text;

        const wrapper = document.createElement("span");
        wrapper.className = "select2-image-option";
        const image = document.createElement("img");
        image.src = imagePath;
        image.alt = "";
        image.loading = "lazy";
        const label = document.createElement("span");
        label.textContent = state.text || "";
        wrapper.append(image, label);
        return wrapper;
    };

    const renderImageSelection = (state) => {
        const imagePath = state.element?.dataset?.image;
        if (!imagePath) return state.text;

        const wrapper = document.createElement("span");
        wrapper.className = "select2-image-selection";
        const image = document.createElement("img");
        image.src = imagePath;
        image.alt = "";
        const label = document.createElement("span");
        label.textContent = state.text || "";
        wrapper.append(image, label);
        return wrapper;
    };

    const initOne = (select) => {
        if (select.closest("template") || select.classList.contains("select2-hidden-accessible")) return;

        const firstOption = select.options[0];
        const placeholder = select.dataset.placeholder || (firstOption && (firstOption.value === "" || firstOption.value === "0")
            ? firstOption.textContent?.trim()
            : null);

        jq(select).select2({
            width: "100%",
            minimumResultsForSearch: 0,
            closeOnSelect: !select.multiple,
            placeholder: placeholder || undefined,
            templateResult: renderImageOption,
            templateSelection: renderImageSelection,
            language: {
                errorLoading: () => "Không tải được kết quả.",
                inputTooLong: () => "Từ khóa tìm kiếm quá dài.",
                inputTooShort: () => "Nhập thêm ký tự để tìm kiếm.",
                loadingMore: () => "Đang tải thêm...",
                maximumSelected: () => "Đã chọn đủ số lượng cho phép.",
                noResults: () => "Không tìm thấy lựa chọn phù hợp.",
                searching: () => "Đang tìm kiếm..."
            }
        });
    };

    const init = (root = document) => collect(root).forEach(initOne);
    const destroy = (root) => collect(root).forEach((select) => {
        if (select.classList.contains("select2-hidden-accessible")) jq(select).select2("destroy");
    });
    const refresh = (select) => {
        if (!(select instanceof HTMLSelectElement)) return;
        if (!select.classList.contains("select2-hidden-accessible")) initOne(select);
        else jq(select).trigger("change.select2");
    };

    window.WareHouseSelect2 = { init, destroy, refresh };
    init(document);

    jq(document).on("select2:open", () => {
        requestAnimationFrame(() =>
            document.querySelector(".select2-container--open .select2-search__field")?.focus());
    });

    // Select2 emits a jQuery change event. Relay a native event as well so the
    // warehouse/payment filters that use addEventListener keep working.
    jq(document).on("select2:select select2:unselect select2:clear", selector, function () {
        this.dispatchEvent(new Event("change", { bubbles: true }));
    });

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => mutation.addedNodes.forEach((node) => {
            if (node instanceof Element) init(node);
        }));
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();

(() => {
    "use strict";

    const restoreSubmittingState = () => {
        document.querySelectorAll('form[data-submitting="true"]').forEach((form) => {
            delete form.dataset.submitting;
            form.removeAttribute("aria-busy");
        });
        document.querySelectorAll("[data-submit-original-html]").forEach((button) => {
            button.innerHTML = button.dataset.submitOriginalHtml;
            delete button.dataset.submitOriginalHtml;
            button.classList.remove("is-submitting");
            button.removeAttribute("aria-disabled");
        });
        document.querySelectorAll("[data-submit-original-value]").forEach((input) => {
            input.value = input.dataset.submitOriginalValue;
            delete input.dataset.submitOriginalValue;
            input.classList.remove("is-submitting");
            input.removeAttribute("aria-disabled");
        });
    };

    const showSubmitting = (submitter) => {
        if (!(submitter instanceof HTMLElement)) return;

        const currentText = submitter instanceof HTMLInputElement
            ? submitter.value
            : submitter.textContent?.trim() || "";
        const isSaveAction = /lưu|tạo|ghi nhận|đăng nhập|đặt mật khẩu|cập nhật/i.test(currentText);
        const loadingText = submitter.dataset.loadingText ||
            (isSaveAction ? "Đang lưu..." : "Đang xử lý...");

        if (submitter instanceof HTMLInputElement) {
            submitter.dataset.submitOriginalValue = submitter.value;
            submitter.value = loadingText;
        } else {
            submitter.dataset.submitOriginalHtml = submitter.innerHTML;
            const spinner = document.createElement("span");
            spinner.className = "button-loading-spinner";
            spinner.setAttribute("aria-hidden", "true");
            const text = document.createElement("span");
            text.textContent = loadingText;
            submitter.replaceChildren(spinner, text);
        }
        submitter.classList.add("is-submitting");
        submitter.setAttribute("aria-disabled", "true");
    };

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== "post") return;

        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }

        const submitter = event.submitter;
        const message = submitter?.dataset.confirm || form.dataset.confirm;
        if (message && !window.confirm(message)) {
            event.preventDefault();
            return;
        }
        if (event.defaultPrevented) return;

        const skipsValidation = form.noValidate || submitter?.formNoValidate;
        if (!skipsValidation) {
            if (!form.checkValidity()) return;

            const jq = window.jQuery;
            if (jq?.fn?.valid && !jq(form).valid()) return;
        }

        form.dataset.submitting = "true";
        form.setAttribute("aria-busy", "true");
        showSubmitting(submitter);
    });

    window.addEventListener("pageshow", restoreSubmittingState);
})();
