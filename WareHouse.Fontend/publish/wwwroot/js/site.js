(function () {
    'use strict';

    const currency = value => `${new Intl.NumberFormat('vi-VN').format(Number(value) || 0)} đ`;

    function setupProductCards() {
        document.querySelectorAll('[data-product-card]').forEach(card => {
            const image = card.querySelector('[data-product-image]');
            const price = card.querySelector('[data-product-price]');
            const priceCaption = card.querySelector('[data-price-caption]');
            const colorLabel = card.querySelector('[data-color-label]');
            const sizeLabel = card.querySelector('[data-size-label]');
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
                if (price) price.innerHTML = `${new Intl.NumberFormat('vi-VN').format(Number(selectedVariant.price || 0))} <i>đ</i>`;
                if (priceCaption) priceCaption.textContent = 'Giá';
                if (colorLabel) colorLabel.textContent = selectedVariant.colorName || '';
                if (sizeLabel) sizeLabel.textContent = selectedVariant.sizeName || '';
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
        const price = Number(variant.salePrice || 0);
        const stock = Number(variant.stock || 0);
        const priceElement = document.querySelector('[data-detail-price]');
        const stockElement = document.querySelector('[data-detail-stock]');
        const message = document.querySelector('[data-combination-message]');

        if (priceElement) priceElement.textContent = currency(price);
        if (stockElement) stockElement.textContent = stock > 0
            ? `Còn ${new Intl.NumberFormat('vi-VN').format(stock)} tại cửa hàng`
            : 'Mẫu này nhận đặt trước';
        if (variant.imageUrl) {
            const cover = document.querySelector('[data-detail-cover] img');
            if (cover) cover.src = variant.imageUrl;
        }
        if (message) {
            message.textContent = `${variant.label || 'Mẫu tiêu chuẩn'} · ${stock > 0 ? `còn ${new Intl.NumberFormat('vi-VN').format(stock)}` : 'nhận đặt trước'}`;
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
                if (label) label.textContent = active?.dataset.valueName || 'Chưa chọn';
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
                    message.textContent = 'Chọn đủ phân loại để xem giá và tình trạng hàng.';
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

    function setupGlobalActions() {
        document.addEventListener('click', event => {
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
    setupGlobalActions();
}());
