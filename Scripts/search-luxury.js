
(function () {
    function initLuxurySearch() {
        const searchInput = document.getElementById('searchInput');
        const searchForm = document.querySelector('.search-box');
        const searchArea = document.querySelector('.search-area');

        if (!searchInput || !searchForm || !searchArea) return;

        const style = document.createElement('style');
        style.textContent = `
            .search-area {
                position: relative;
                z-index: 20;
            }

            .luxury-search-wrap {
                position: relative;
                width: 100%;
                max-width: 750px;
            }

            .luxury-search-wrap .search-box {
                max-width: none;
            }

            .luxury-search-panel {
                display: none;
                position: absolute;
                top: calc(100% + 10px);
                left: 0;
                width: 100%;
                padding: 23px;
                background: #fff;
                border: 1px solid #e8dfcb;
                border-radius: 18px;
                box-shadow: 0 15px 40px rgba(0,0,0,.13);
                z-index: 30;
            }

            .luxury-search-panel.open {
                display: block;
            }

            .luxury-search-title {
                font-weight: bold;
                font-size: 14px;
                margin-bottom: 13px;
            }

            .luxury-search-tags {
                display: flex;
                flex-wrap: wrap;
                gap: 9px;
                margin-bottom: 23px;
            }

            .luxury-search-tag {
                border: 1px solid #dfd4b9;
                background: #faf9f6;
                border-radius: 20px;
                padding: 9px 16px;
                cursor: pointer;
            }

            .luxury-search-tag:hover {
                background: #111;
                color: #d4af37;
            }

            .luxury-price-row {
                display: flex;
                gap: 12px;
                align-items: center;
            }

            .luxury-price-row input {
                width: 50%;
                min-width: 0;
                padding: 13px;
                border: 1px solid #ddd;
                border-radius: 9px;
                font-size: 14px;
            }

            .luxury-search-bottom {
                display: flex;
                justify-content: flex-end;
                gap: 10px;
                margin-top: 18px;
            }

            .luxury-search-bottom button {
                padding: 11px 18px;
                border-radius: 8px;
                cursor: pointer;
                font-weight: bold;
            }

            .luxury-reset {
                background: white;
                border: 1px solid #ccc;
            }

            .luxury-apply {
                background: #d4af37;
                border: 1px solid #d4af37;
                color: #111;
            }
        `;
        document.head.appendChild(style);

        const wrapper = document.createElement('div');
        wrapper.className = 'luxury-search-wrap';

        searchArea.insertBefore(wrapper, searchForm);
        wrapper.appendChild(searchForm);

        const panel = document.createElement('div');
        panel.className = 'luxury-search-panel';

        panel.innerHTML = `
            <div class="luxury-search-title">Tìm kiếm phổ biến</div>

            <div class="luxury-search-tags">
                <button type="button" class="luxury-search-tag" data-keyword="Áo">Áo</button>
                <button type="button" class="luxury-search-tag" data-keyword="Quần">Quần</button>
                <button type="button" class="luxury-search-tag" data-keyword="Phụ kiện">Phụ kiện</button>
                <button type="button" class="luxury-search-tag" data-keyword="Thời trang">Thời trang</button>
            </div>

            <div class="luxury-search-title">Lọc theo khoảng giá (VNĐ)</div>

            <div class="luxury-price-row">
                <input type="number" id="luxuryMinPrice"
                       min="0" placeholder="Giá từ" />
                <span>—</span>
                <input type="number" id="luxuryMaxPrice"
                       min="0" placeholder="Giá đến" />
            </div>

            <div class="luxury-search-bottom">
                <button type="button" class="luxury-reset" id="luxuryReset">
                    Xóa bộ lọc
                </button>

                <button type="button" class="luxury-apply" id="luxuryApply">
                    Tìm kiếm
                </button>
            </div>
        `;

        wrapper.appendChild(panel);

        const minInput = document.getElementById('luxuryMinPrice');
        const maxInput = document.getElementById('luxuryMaxPrice');

        let minPrice = null;
        let maxPrice = null;

        function normalizeText(text) {
            return String(text || '')
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '')
                .replace(/đ/g, 'd')
                .toLowerCase()
                .trim();
        }

        function applySearch() {
            const minValue = minInput.value.trim();
            const maxValue = maxInput.value.trim();

            minPrice = minValue === '' ? null : Number(minValue);
            maxPrice = maxValue === '' ? null : Number(maxValue);

            if ((minPrice !== null && minPrice < 0) ||
                (maxPrice !== null && maxPrice < 0)) {
                alert('Giá không được nhỏ hơn 0.');
                return;
            }

            if (minPrice !== null && maxPrice !== null &&
                minPrice > maxPrice) {
                alert('Giá từ không được lớn hơn giá đến.');
                return;
            }

            searchKeyword = normalizeText(searchInput.value);
            showAll = true;
            renderProducts();
            panel.classList.remove('open');
        }

        // Thay chức năng lọc cũ bằng tìm kiếm nâng cao
        getFilteredProducts = function () {
            return products.filter(function (p) {
                const categoryMatch =
                    selectedCategory === 'all' ||
                    p.category === selectedCategory;

                const keyword = normalizeText(searchKeyword);

                const keywordMatch =
                    normalizeText(p.name).includes(keyword) ||
                    normalizeText(p.category).includes(keyword);

                const minMatch =
                    minPrice === null || p.price >= minPrice;

                const maxMatch =
                    maxPrice === null || p.price <= maxPrice;

                return categoryMatch &&
                    keywordMatch &&
                    minMatch &&
                    maxMatch;
            });
        };

        searchProducts = function (event) {
            if (event) event.preventDefault();
            applySearch();
        };

        searchInput.addEventListener('focus', function () {
            panel.classList.add('open');
        });

        panel.querySelectorAll('[data-keyword]').forEach(function (button) {
            button.addEventListener('click', function () {
                searchInput.value = button.dataset.keyword;
                applySearch();
            });
        });

        document.getElementById('luxuryApply')
            .addEventListener('click', applySearch);

        document.getElementById('luxuryReset')
            .addEventListener('click', function () {
                searchInput.value = '';
                minInput.value = '';
                maxInput.value = '';

                minPrice = null;
                maxPrice = null;
                searchKeyword = '';
                showAll = false;

                renderProducts();
                panel.classList.remove('open');
            });

        document.addEventListener('click', function (event) {
            if (!wrapper.contains(event.target)) {
                panel.classList.remove('open');
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                panel.classList.remove('open');
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLuxurySearch);
    } else {
        initLuxurySearch();
    }
})();
