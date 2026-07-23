(function () {
    'use strict';
    const i18n = window.storeI18n || {};
    const formatNumber = value => new Intl.NumberFormat(i18n.locale || 'vi-VN').format(value);
    const formatText = (template, ...values) => values.reduce(
        (text, value, index) => text.replace(`{${index}}`, value),
        template || '');

    function setupProductCards() {
        document.querySelectorAll('[data-product-card]').forEach(card => {
            const image = card.querySelector('[data-product-image]');
            const colorLabel = card.querySelector('[data-color-label]');
            const json = card.querySelector('[data-card-variants]');
            let variants = [];
            try { variants = JSON.parse(json?.textContent || '[]'); } catch { variants = []; }
            if (!variants.length) return;

            const hasColors = variants.some(variant => variant.colorValueId != null);
            const hasSizes = variants.some(variant => variant.sizeValueId != null);
            const initial = variants.find(variant => Number(variant.stock) > 0) || variants[0];
            let selectedColor = initial.colorValueId == null ? null : Number(initial.colorValueId);
            let selectedSize = initial.sizeValueId == null ? null : Number(initial.sizeValueId);
            let committedColor = selectedColor;
            let committedSize = selectedSize;

            const matches = (variant, colorId, sizeId) =>
                (!hasColors || Number(variant.colorValueId) === colorId)
                && (!hasSizes || Number(variant.sizeValueId) === sizeId);

            const render = () => {
                let selectedVariant = variants.find(variant => matches(variant, selectedColor, selectedSize));
                selectedVariant ||= variants.find(variant => !hasColors || Number(variant.colorValueId) === selectedColor);
                selectedVariant ||= variants[0];

                card.querySelectorAll('[data-card-color]').forEach(button => {
                    const valueId = Number(button.dataset.cardColor);
                    const active = valueId === selectedColor;
                    button.classList.toggle('active', active);
                    button.setAttribute('aria-pressed', String(active));
                    if (!active && document.activeElement === button) button.blur();
                });
                card.querySelectorAll('[data-card-size]').forEach(button => {
                    const valueId = Number(button.dataset.cardSize);
                    const available = variants.some(variant =>
                        Number(variant.sizeValueId) === valueId
                        && (!hasColors || Number(variant.colorValueId) === selectedColor));
                    const active = available && valueId === selectedSize;
                    button.disabled = !available;
                    button.classList.toggle('active', active);
                    button.setAttribute('aria-pressed', String(active));
                    if (!active && document.activeElement === button) button.blur();
                });

                if (image && selectedVariant.imageUrl) image.src = selectedVariant.imageUrl;
                if (colorLabel) colorLabel.textContent = selectedVariant.colorName || '';
            };

            card.querySelectorAll('[data-card-color]').forEach(button => {
                const preview = () => {
                    selectedColor = Number(button.dataset.cardColor);
                    const colorVariants = variants.filter(variant => Number(variant.colorValueId) === selectedColor);
                    if (hasSizes && !colorVariants.some(variant => Number(variant.sizeValueId) === selectedSize)) {
                        const preferred = colorVariants.find(variant => Number(variant.stock) > 0) || colorVariants[0];
                        selectedSize = preferred?.sizeValueId == null ? null : Number(preferred.sizeValueId);
                    }
                    render();
                };
                button.addEventListener('mouseenter', preview);
                button.addEventListener('focus', preview);
                button.addEventListener('click', () => {
                    preview();
                    committedColor = selectedColor;
                    committedSize = selectedSize;
                    button.blur();
                });
            });

            card.querySelectorAll('[data-card-size]').forEach(button => {
                const preview = () => {
                    selectedSize = Number(button.dataset.cardSize);
                    const exact = variants.find(variant => matches(variant, selectedColor, selectedSize));
                    if (!exact && hasColors) {
                        const sizeVariant = variants.find(variant => Number(variant.sizeValueId) === selectedSize);
                        selectedColor = sizeVariant?.colorValueId == null ? null : Number(sizeVariant.colorValueId);
                    }
                    render();
                };
                button.addEventListener('mouseenter', preview);
                button.addEventListener('focus', preview);
                button.addEventListener('click', () => {
                    preview();
                    committedColor = selectedColor;
                    committedSize = selectedSize;
                    button.blur();
                });
            });

            card.addEventListener('mouseleave', () => {
                selectedColor = committedColor;
                selectedSize = committedSize;
                render();
            });
            render();
        });
    }

    function applyVariant(variant) {
        const stock = Number(variant.stock || 0);
        const stockElement = document.querySelector('[data-detail-stock]');
        const message = document.querySelector('[data-combination-message]');

        if (stockElement) stockElement.textContent = stock > 0
            ? formatText(i18n.stockOnly, formatNumber(stock))
            : i18n.selectedModelPreOrder;
        if (variant.imageUrl) {
            const cover = document.querySelector('[data-detail-cover] img');
            if (cover) cover.src = variant.imageUrl;
        }
        if (message) {
            message.textContent = stock > 0
                ? formatText(i18n.selectedVariantInStock, variant.label || i18n.standardModel, formatNumber(stock))
                : formatText(i18n.selectedVariantPreOrder, variant.label || i18n.standardModel);
            message.classList.add('is-ready');
        }
    }

    function setupCombinationPicker() {
        const json = document.querySelector('#productVariants');
        if (!json) return;

        let variants = [];
        try { variants = JSON.parse(json.textContent || '[]'); } catch { variants = []; }
        if (!variants.length) return;

        const picker = document.querySelector('[data-combination-picker]');
        if (!picker) {
            applyVariant(variants[0]);
            return;
        }

        const groupIds = [...picker.querySelectorAll('[data-attribute-group]')].map(group => Number(group.dataset.attributeGroup));
        const selected = new Map();
        const initial = variants.find(item => Number(item.stock) > 0) || variants[0];
        initial.attributeValueIds.forEach(valueId => {
            const button = picker.querySelector(`[data-value-id="${valueId}"]`);
            if (button) selected.set(Number(button.dataset.groupId), Number(valueId));
        });

        const render = () => {
            picker.querySelectorAll('[data-value-id]').forEach(button => {
                const groupId = Number(button.dataset.groupId);
                const valueId = Number(button.dataset.valueId);
                const proposed = new Map(selected);
                proposed.set(groupId, valueId);
                const possible = variants.some(variant => [...proposed.values()].every(id => variant.attributeValueIds.includes(id)));
                button.disabled = !possible;
                const isSelected = selected.get(groupId) === valueId;
                button.classList.toggle('active', isSelected);
                button.setAttribute('aria-pressed', String(isSelected));
            });

            picker.querySelectorAll('[data-attribute-group]').forEach(group => {
                const groupId = Number(group.dataset.attributeGroup);
                const active = group.querySelector(`[data-value-id="${selected.get(groupId)}"]`);
                const label = group.querySelector('[data-selected-label]');
                if (label) label.textContent = active?.dataset.valueName || i18n.notSelected;
            });

            const complete = groupIds.every(id => selected.has(id));
            const variant = complete
                ? variants.find(item => [...selected.values()].every(id => item.attributeValueIds.includes(id)))
                : null;
            if (variant) {
                applyVariant(variant);
            } else {
                const message = document.querySelector('[data-combination-message]');
                if (message) {
                    message.textContent = i18n.chooseVariant;
                    message.classList.remove('is-ready');
                }
            }
        };

        picker.addEventListener('click', event => {
            const button = event.target.closest('[data-value-id]');
            if (!button || button.disabled) return;
            selected.set(Number(button.dataset.groupId), Number(button.dataset.valueId));
            render();
        });
        render();
    }

    function setupSiteSearch() {
        document.querySelectorAll('[data-site-search]').forEach(form => {
            const input = form.querySelector('input[name="q"]');
            const suggestions = form.querySelector('[data-search-suggestions]');
            if (!input || !suggestions) return;

            let timer = 0;
            let request = null;
            let activeIndex = -1;

            const close = () => {
                suggestions.hidden = true;
                suggestions.replaceChildren();
                input.setAttribute('aria-expanded', 'false');
                activeIndex = -1;
            };

            const setActive = index => {
                const options = [...suggestions.querySelectorAll('a[role="option"]')];
                if (!options.length) return;
                activeIndex = (index + options.length) % options.length;
                options.forEach((option, optionIndex) => {
                    const isActive = optionIndex === activeIndex;
                    option.classList.toggle('active', isActive);
                    option.setAttribute('aria-selected', String(isActive));
                });
                options[activeIndex].scrollIntoView({ block: 'nearest' });
            };

            const showMessage = message => {
                const item = document.createElement('div');
                item.className = 'search-suggestion-message';
                item.textContent = message;
                suggestions.replaceChildren(item);
                suggestions.hidden = false;
                input.setAttribute('aria-expanded', 'true');
            };

            const render = items => {
                suggestions.replaceChildren();
                activeIndex = -1;
                if (!items.length) {
                    showMessage(i18n.noSuggestions);
                    return;
                }

                items.forEach(item => {
                    const link = document.createElement('a');
                    link.className = 'search-suggestion-item';
                    link.href = item.productUrl;
                    link.setAttribute('role', 'option');
                    link.setAttribute('aria-selected', 'false');

                    const media = document.createElement('span');
                    media.className = 'search-suggestion-media';
                    if (item.imageUrl) {
                        const image = document.createElement('img');
                        image.src = item.imageUrl;
                        image.alt = '';
                        media.appendChild(image);
                    } else {
                        media.textContent = 'TA';
                    }

                    const text = document.createElement('span');
                    text.className = 'search-suggestion-text';
                    const name = document.createElement('strong');
                    name.textContent = item.name;
                    text.appendChild(name);
                    if (item.category) {
                        const category = document.createElement('small');
                        category.textContent = item.category;
                        text.appendChild(category);
                    }

                    const arrow = document.createElement('span');
                    arrow.className = 'search-suggestion-arrow';
                    arrow.textContent = '›';
                    link.append(media, text, arrow);
                    suggestions.appendChild(link);
                });

                suggestions.hidden = false;
                input.setAttribute('aria-expanded', 'true');
            };

            const loadSuggestions = () => {
                const query = input.value.trim();
                window.clearTimeout(timer);
                request?.abort();
                if (query.length < 2) {
                    close();
                    return;
                }

                timer = window.setTimeout(async () => {
                    request = new AbortController();
                    showMessage(i18n.searching);
                    try {
                        const endpoint = form.dataset.suggestionsUrl || '/api/tim-kiem-goi-y';
                        const response = await fetch(`${endpoint}?q=${encodeURIComponent(query)}`, {
                            headers: { Accept: 'application/json' },
                            signal: request.signal
                        });
                        if (!response.ok) throw new Error('Search request failed');
                        render(await response.json());
                    } catch (error) {
                        if (error.name !== 'AbortError') close();
                    }
                }, 220);
            };

            input.addEventListener('input', loadSuggestions);
            input.addEventListener('focus', () => {
                if (input.value.trim().length >= 2 && suggestions.childElementCount === 0)
                    loadSuggestions();
            });
            input.addEventListener('keydown', event => {
                const options = [...suggestions.querySelectorAll('a[role="option"]')];
                if (event.key === 'ArrowDown' && options.length) {
                    event.preventDefault();
                    setActive(activeIndex + 1);
                } else if (event.key === 'ArrowUp' && options.length) {
                    event.preventDefault();
                    setActive(activeIndex - 1);
                } else if (event.key === 'Enter' && activeIndex >= 0 && options[activeIndex]) {
                    event.preventDefault();
                    window.location.href = options[activeIndex].href;
                } else if (event.key === 'Escape') {
                    close();
                }
            });
            form.addEventListener('submit', event => {
                if (!input.value.trim()) {
                    event.preventDefault();
                    input.focus();
                }
            });
            document.addEventListener('click', event => {
                if (!form.contains(event.target)) close();
            });
        });
    }

    function setupGlobalActions() {
        document.addEventListener('click', event => {
            const mobileNavClose = event.target.closest('[data-mobile-nav-close]');
            if (mobileNavClose) {
                const menu = document.querySelector('#storeNav');
                if (menu && window.bootstrap?.Offcanvas) {
                    event.preventDefault();
                    window.bootstrap.Offcanvas.getOrCreateInstance(menu).hide();
                }
                return;
            }

            const mobileNavLink = event.target.closest('#storeNav a');
            if (mobileNavLink && window.innerWidth < 992) {
                const menu = document.querySelector('#storeNav');
                if (menu && window.bootstrap?.Offcanvas) {
                    window.bootstrap.Offcanvas.getOrCreateInstance(menu).hide();
                }
            }

            const thumb = event.target.closest('[data-gallery-thumb]');
            if (thumb) {
                const cover = document.querySelector('[data-detail-cover] img');
                if (cover) cover.src = thumb.dataset.galleryThumb;
                document.querySelectorAll('[data-gallery-thumb]').forEach(item => item.classList.toggle('active', item === thumb));
                return;
            }

            if (event.target.closest('[data-scroll-top]')) {
                window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
            }
        });
    }

    setupProductCards();
    setupCombinationPicker();
    setupSiteSearch();
    setupGlobalActions();
}());
