
(function () {
    "use strict";

    const PRODUCT_API = "/ProductApi/Index";
    const ORDER_API = "/OrderApi/Create";
    const CART_KEY = "MINI_SHOP_LUXURY_CART";

    let products = [];
    let cart = [];

    let selectedCategory = "all";
    let searchKeyword = "";
    let selectedMin = null;
    let selectedMax = null;
    let showAll = false;
    let submittingOrder = false;

    function formatPrice(value) {
        return Number(value).toLocaleString("vi-VN") + " ₫";
    }

    function normalizeText(value) {
        return String(value || "")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/đ/g, "d")
            .toLowerCase()
            .trim();
    }

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getImageUrl(product) {
        const image = String(product.image || "").trim();

        if (!image) {
            return "";
        }

        if (
            image.startsWith("https://") ||
            image.startsWith("http://") ||
            image.startsWith("/")
        ) {
            return image;
        }

        if (image.startsWith("~/")) {
            return image.substring(1);
        }

        return "/Content/images san_pham/" + image;
    }

    function findProduct(id) {
        return products.find(function (p) {
            return p.id === Number(id);
        });
    }

    async function loadProducts() {
        const grid = document.getElementById("productGrid");

        if (grid) {
            grid.innerHTML =
                '<p class="empty-message">Đang tải sản phẩm...</p>';
        }

        try {
            const response = await fetch(PRODUCT_API, {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store"
            });

            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }

            const data = await response.json();

            if (!Array.isArray(data)) {
                throw new Error("Dữ liệu sản phẩm không hợp lệ.");
            }

            products = data.map(function (item) {
                return {
                    id: Number(item.id),
                    name: String(item.name || ""),
                    category: String(item.category || ""),
                    price: Number(item.price),
                    stock: Number(item.stock),
                    image: String(item.image || "")
                };
            }).filter(function (p) {
                return Number.isInteger(p.id) &&
                    p.id > 0 &&
                    Number.isFinite(p.price) &&
                    p.price >= 0 &&
                    Number.isInteger(p.stock) &&
                    p.stock >= 0;
            });

            loadCart();
            renderProducts();

        } catch (error) {
            console.error("Lỗi tải sản phẩm:", error);

            products = [];
            cart = [];
            updateCartCount();

            if (grid) {
                grid.innerHTML =
                    '<p class="empty-message">' +
                    'Không thể tải sản phẩm từ SQL Server.' +
                    '</p>';
            }
        }
    }

    function updateCartCount() {
        const element = document.getElementById("cartCount");

        if (element) {
            element.textContent = cart.reduce(function (sum, item) {
                return sum + item.quantity;
            }, 0);
        }
    }

    function loadCart() {
        try {
            const saved = JSON.parse(
                localStorage.getItem(CART_KEY) || "[]"
            );

            if (!Array.isArray(saved)) {
                cart = [];
            } else {
                cart = saved.filter(function (item) {
                    const p = findProduct(item.id);

                    return p &&
                        Number.isInteger(item.quantity) &&
                        item.quantity > 0 &&
                        p.stock > 0;
                }).map(function (item) {
                    const p = findProduct(item.id);

                    return {
                        id: Number(item.id),
                        quantity: Math.min(item.quantity, p.stock)
                    };
                });
            }
        } catch (error) {
            cart = [];
        }

        saveCart();
    }

    function saveCart() {
        localStorage.setItem(CART_KEY, JSON.stringify(cart));
        updateCartCount();
    }

    function getFilteredProducts() {
        return products.filter(function (p) {
            const matchCategory =
                selectedCategory === "all" ||
                p.category === selectedCategory;

            const matchSearch =
                normalizeText(p.name).includes(searchKeyword) ||
                normalizeText(p.category).includes(searchKeyword);

            const matchMin =
                selectedMin === null || p.price >= selectedMin;

            const matchMax =
                selectedMax === null || p.price <= selectedMax;

            return matchCategory &&
                matchSearch &&
                matchMin &&
                matchMax;
        });
    }

    function renderProducts() {
        const grid = document.getElementById("productGrid");

        if (!grid) {
            return;
        }

        const filtered = getFilteredProducts();
        const visible = showAll
            ? filtered
            : filtered.slice(0, 6);

        if (visible.length === 0) {
            grid.innerHTML =
                '<p class="empty-message">' +
                'Không tìm thấy sản phẩm phù hợp.' +
                '</p>';
        } else {
            grid.innerHTML = visible.map(function (p) {
                const image = escapeHtml(getImageUrl(p));
                const name = escapeHtml(p.name);
                const category = escapeHtml(p.category);
                const soldOut = p.stock <= 0;

                return `
                    <div class="product-card">

                        <img class="product-image"
                             src="${image}"
                             alt="${name}"
                             ondblclick="showDetail(${p.id})"
                             title="Nhấp đúp để xem chi tiết" />

                        <div class="image-hint">
                            Nhấp đúp vào ảnh để xem chi tiết
                        </div>

                        <div class="product-info">

                            <div class="product-category">
                                ${category}
                            </div>

                            <div class="product-name">
                                ${name}
                            </div>

                            <div class="product-price">
                                ${formatPrice(p.price)}
                            </div>

                            <div class="product-actions">

                                <button class="btn-detail"
                                        onclick="addToCart(${p.id})"
                                        ${soldOut ? "disabled" : ""}>
                                    <i class="fa-solid fa-cart-plus"></i>
                                    ${soldOut ? "Hết hàng" : "Thêm vào giỏ hàng"}
                                </button>

                                <button class="btn-buy"
                                        onclick="buyNow(${p.id})"
                                        ${soldOut ? "disabled" : ""}>
                                    <i class="fa-solid fa-bag-shopping"></i>
                                    ${soldOut ? "Hết hàng" : "Mua"}
                                </button>

                            </div>

                        </div>

                    </div>
                `;
            }).join("");
        }

        const moreBtn = document.getElementById("moreBtn");

        if (moreBtn) {
            moreBtn.style.display =
                !showAll && filtered.length > 6
                    ? "inline-block"
                    : "none";
        }
    }

    function filterCategory(category, button) {
        selectedCategory = category;
        showAll = false;

        document.querySelectorAll(".category-btn")
            .forEach(function (b) {
                b.classList.remove("active");
            });

        if (button) {
            button.classList.add("active");
        }

        renderProducts();
    }

    function applySearch() {
        const minInput = document.getElementById("minPrice");
        const maxInput = document.getElementById("maxPrice");
        const searchInput = document.getElementById("searchInput");

        const a = minInput ? minInput.value.trim() : "";
        const b = maxInput ? maxInput.value.trim() : "";

        const min = a === "" ? null : Number(a);
        const max = b === "" ? null : Number(b);

        if (
            (min !== null && (!Number.isFinite(min) || min < 0)) ||
            (max !== null && (!Number.isFinite(max) || max < 0))
        ) {
            alert("Giá không hợp lệ.");
            return;
        }

        if (min !== null && max !== null && min > max) {
            alert("Giá từ không được lớn hơn giá đến.");
            return;
        }

        selectedMin = min;
        selectedMax = max;

        searchKeyword = normalizeText(
            searchInput ? searchInput.value : ""
        );

        showAll = true;
        renderProducts();

        const panel = document.getElementById("searchPanel");

        if (panel) {
            panel.classList.remove("open");
        }
    }

    function searchProducts(event) {
        if (event) {
            event.preventDefault();
        }

        applySearch();
    }

    function chooseKeyword(keyword) {
        const input = document.getElementById("searchInput");

        if (input) {
            input.value = keyword;
        }

        applySearch();
    }

    function resetSearch() {
        ["searchInput", "minPrice", "maxPrice"].forEach(function (id) {
            const input = document.getElementById(id);

            if (input) {
                input.value = "";
            }
        });

        searchKeyword = "";
        selectedMin = null;
        selectedMax = null;
        selectedCategory = "all";
        showAll = false;

        document.querySelectorAll(".category-btn")
            .forEach(function (button) {
                button.classList.remove("active");
            });

        const firstButton = document.querySelector(".category-btn");

        if (firstButton) {
            firstButton.classList.add("active");
        }

        const panel = document.getElementById("searchPanel");

        if (panel) {
            panel.classList.remove("open");
        }

        renderProducts();
    }

    function showMore() {
        showAll = true;
        renderProducts();
    }

    function openModal(id) {
        const modal = document.getElementById(id);

        if (!modal) {
            return;
        }

        modal.style.display = "flex";
        document.body.classList.add("modal-open");
    }

    function hideModal(id) {
        const modal = document.getElementById(id);

        if (!modal) {
            return;
        }

        modal.style.display = "none";

        const anyOpen = Array.from(
            document.querySelectorAll(".modal")
        ).some(function (m) {
            return m.style.display === "flex";
        });

        if (!anyOpen) {
            document.body.classList.remove("modal-open");
        }
    }

    function closeModal(event, id) {
        if (event.target.id === id) {
            hideModal(id);
        }
    }

    function showDetail(id) {
        const p = findProduct(id);

        if (!p) {
            return;
        }

        const body = document.getElementById("detailBody");

        if (!body) {
            return;
        }

        body.innerHTML = `
            <img src="${escapeHtml(getImageUrl(p))}"
                 alt="${escapeHtml(p.name)}" />

            <h2>${escapeHtml(p.name)}</h2>

            <p>Danh mục: ${escapeHtml(p.category)}</p>

            <h3 style="color:#111">
                ${formatPrice(p.price)}
            </h3>

            <p>
                Sản phẩm thuộc bộ sưu tập MINI SHOP & LUXURY.
            </p>

            <p>
                ${p.stock > 0
                ? "Còn hàng: " + p.stock
                : "Sản phẩm đã hết hàng"}
            </p>

            <button class="btn-buy"
                    style="width:100%"
                    onclick="addToCart(${p.id})"
                    ${p.stock <= 0 ? "disabled" : ""}>
                <i class="fa-solid fa-cart-plus"></i>
                ${p.stock <= 0 ? "Hết hàng" : "Thêm vào giỏ hàng"}
            </button>
        `;

        openModal("detailModal");
    }

    function showToast(message) {
        const toast = document.getElementById("toast");

        if (!toast) {
            return;
        }

        toast.textContent = message;
        toast.style.display = "block";

        setTimeout(function () {
            toast.style.display = "none";
        }, 2500);
    }

    function addToCart(id) {
        const p = findProduct(id);

        if (!p) {
            return;
        }

        if (p.stock <= 0) {
            alert("Sản phẩm đã hết hàng.");
            return;
        }

        const item = cart.find(function (x) {
            return x.id === p.id;
        });

        if (item) {
            if (item.quantity >= p.stock) {
                alert("Số lượng trong giỏ đã đạt mức tồn kho.");
                return;
            }

            item.quantity++;
        } else {
            cart.push({
                id: p.id,
                quantity: 1
            });
        }

        saveCart();
        showToast("Đã thêm " + p.name + " vào giỏ hàng!");
    }

    function buyNow(id) {
        const p = findProduct(id);

        if (!p || p.stock <= 0) {
            alert("Sản phẩm hiện không còn hàng.");
            return;
        }

        const item = cart.find(function (x) {
            return x.id === p.id;
        });

        if (item) {
            if (item.quantity >= p.stock) {
                alert("Số lượng đã đạt mức tồn kho.");
                return;
            }

            item.quantity++;
        } else {
            cart.push({
                id: p.id,
                quantity: 1
            });
        }

        saveCart();
        goCheckout();
    }

    function changeQuantity(id, change) {
        const item = cart.find(function (x) {
            return x.id === Number(id);
        });

        if (!item) {
            return;
        }

        const p = findProduct(id);

        if (!p) {
            return;
        }

        const newQuantity = item.quantity + Number(change);

        if (newQuantity > p.stock) {
            alert("Không đủ số lượng sản phẩm trong kho.");
            return;
        }

        if (newQuantity <= 0) {
            cart = cart.filter(function (x) {
                return x.id !== Number(id);
            });
        } else {
            item.quantity = newQuantity;
        }

        saveCart();
        renderCart();
    }

    function removeFromCart(id) {
        cart = cart.filter(function (x) {
            return x.id !== Number(id);
        });

        saveCart();
        renderCart();
    }

    function cartTotal() {
        return cart.reduce(function (sum, item) {
            const p = findProduct(item.id);

            return sum + (p ? p.price * item.quantity : 0);
        }, 0);
    }

    function renderCart() {
        const container = document.getElementById("cartItems");
        const summary = document.getElementById("cartSummary");

        if (!container || !summary) {
            return;
        }

        if (cart.length === 0) {
            container.innerHTML = `
                <div class="cart-empty">
                    <i class="fa-solid fa-cart-shopping"></i>
                    <h3>Giỏ hàng đang trống</h3>
                    <p>Hãy chọn sản phẩm bạn yêu thích!</p>
                </div>
            `;

            summary.style.display = "none";
            return;
        }

        summary.style.display = "block";

        container.innerHTML = cart.map(function (item) {
            const p = findProduct(item.id);

            if (!p) {
                return "";
            }

            return `
                <div class="cart-item">

                    <img src="${escapeHtml(getImageUrl(p))}"
                         alt="${escapeHtml(p.name)}" />

                    <div class="cart-item-info">

                        <div class="cart-item-name">
                            ${escapeHtml(p.name)}
                        </div>

                        <div class="cart-item-price">
                            ${formatPrice(p.price)}
                        </div>

                        <div class="quantity-controls">

                            <button onclick="changeQuantity(${p.id},-1)">
                                −
                            </button>

                            <span>${item.quantity}</span>

                            <button onclick="changeQuantity(${p.id},1)">
                                +
                            </button>

                        </div>

                    </div>

                    <button class="remove-cart"
                            onclick="removeFromCart(${p.id})">
                        <i class="fa-solid fa-trash"></i>
                    </button>

                </div>
            `;
        }).join("");

        const totalElement = document.getElementById("cartTotal");

        if (totalElement) {
            totalElement.textContent = formatPrice(cartTotal());
        }
    }

    function showCart() {
        renderCart();
        openModal("cartModal");
    }

    function goCheckout() {
        if (cart.length === 0) {
            alert("Giỏ hàng đang trống!");
            return;
        }

        hideModal("cartModal");
        renderCheckout();
        renderPaymentMethods();
        openModal("checkoutModal");
    }

    function renderCheckout() {
        const itemsElement = document.getElementById("checkoutItems");
        const totalElement = document.getElementById("checkoutTotal");

        if (itemsElement) {
            itemsElement.innerHTML = cart.map(function (item) {
                const p = findProduct(item.id);

                if (!p) {
                    return "";
                }

                return `
                    <p>
                        ${escapeHtml(p.name)}
                        × ${item.quantity}
                        — ${formatPrice(p.price * item.quantity)}
                    </p>
                `;
            }).join("");
        }

        if (totalElement) {
            totalElement.textContent = formatPrice(cartTotal());
        }
    }

    // KHONG TAO THEM PHAN THANH TOAN
    // CHI GIU GIAO DIEN COD / VIETQR TRONG INDEX.CSHTML
    function renderPaymentMethods() {
        const duplicateBox = document.getElementById(
            "paymentMethodBox"
        );

        if (duplicateBox) {
            duplicateBox.remove();
        }
    }

    // DOC TRUC TIEP RADIO COD / VIETQR
    function getPaymentMethod() {
        const form = document.getElementById("checkoutForm");

        if (!form) {
            return null;
        }

        const radio = form.querySelector(
            'input[name="paymentChoice"]:checked'
        );

        if (radio) {
            const value = String(radio.value || "")
                .trim()
                .toUpperCase();

            if (value === "BANK") {
                return "BANK";
            }

            if (value === "COD") {
                return "COD";
            }
        }

        const select = form.querySelector(
            'select[name="PaymentMethod"], ' +
            'select[name="paymentMethod"], ' +
            '#paymentMethod, ' +
            '#PaymentMethod'
        );

        if (!select) {
            return null;
        }

        const value = String(select.value || "")
            .trim()
            .toUpperCase();

        if (
            value === "BANK" ||
            value === "BANK_TRANSFER" ||
            value === "VIETQR" ||
            value === "TRANSFER" ||
            value === "BANKING"
        ) {
            return "BANK";
        }

        if (value === "COD") {
            return "COD";
        }

        return null;
    }

    function backToCart() {
        hideModal("checkoutModal");
        showCart();
    }

    function getCheckoutValue(id) {
        const element = document.getElementById(id);

        return element ? element.value.trim() : "";
    }

    function getAntiForgeryToken() {
        const form = document.getElementById("checkoutForm");

        if (!form) {
            return "";
        }

        const token = form.querySelector(
            'input[name="__RequestVerificationToken"]'
        );

        return token ? token.value : "";
    }

    // GUI DON HANG VA TU DONG CHUYEN TRANG
    async function submitOrder(event) {
        if (event) {
            event.preventDefault();
        }

        if (submittingOrder) {
            return;
        }

        if (cart.length === 0) {
            alert("Giỏ hàng đang trống!");
            hideModal("checkoutModal");
            return;
        }

        const customerName = getCheckoutValue("customerName");
        const phone = getCheckoutValue("customerPhone");
        const address = getCheckoutValue("customerAddress");
        const city = getCheckoutValue("customerCity");

        if (
            !customerName ||
            !/^[0-9]{10}$/.test(phone) ||
            !address ||
            !city
        ) {
            alert(
                "Vui lòng nhập đầy đủ thông tin " +
                "và số điện thoại đúng 10 chữ số."
            );
            return;
        }

        const paymentMethod = getPaymentMethod();

        if (!paymentMethod) {
            alert(
                "Không đọc được phương thức thanh toán. " +
                "Vui lòng chọn COD hoặc VietQR BIDV."
            );
            return;
        }

        const token = getAntiForgeryToken();

        if (!token) {
            alert(
                "Thiếu mã bảo mật của biểu mẫu đặt hàng. " +
                "Vui lòng kiểm tra AntiForgeryToken."
            );
            return;
        }

        const formData = new URLSearchParams();

        formData.append("__RequestVerificationToken", token);
        formData.append("CustomerName", customerName);
        formData.append("Phone", phone);
        formData.append("Address", address);
        formData.append("City", city);
        formData.append("PaymentMethod", paymentMethod);

        cart.forEach(function (item, index) {
            formData.append(
                "Items[" + index + "].ProductId",
                String(item.id)
            );

            formData.append(
                "Items[" + index + "].Quantity",
                String(item.quantity)
            );
        });

        submittingOrder = true;

        const form = document.getElementById("checkoutForm");

        const submitButton = form
            ? form.querySelector('button[type="submit"]')
            : null;

        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = "ĐANG XỬ LÝ ĐƠN HÀNG...";
        }

        let orderSaved = false;

        try {
            const response = await fetch(ORDER_API, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type":
                        "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: formData.toString()
            });

            const contentType =
                response.headers.get("content-type") || "";

            if (!contentType.includes("application/json")) {
                throw new Error(
                    "Máy chủ không trả về JSON. HTTP: " +
                    response.status
                );
            }

            const result = await response.json();

            if (!response.ok || !result.success) {
                throw new Error(
                    result.message || "Không thể lưu đơn hàng."
                );
            }

            orderSaved = true;

            // CHI XOA GIO HANG KHI SQL DA LUU THANH CONG
            cart = [];
            saveCart();

            // LAY DUONG DAN TU ORDERAPICONTROLLER
            let redirectUrl = result.redirectUrl;

            // HO TRO PHAN HOI CU NEU CAN
            if (!redirectUrl && result.paymentMethod === "BANK") {
                redirectUrl = result.paymentUrl;
            }

            if (!redirectUrl && result.paymentMethod === "COD") {
                redirectUrl = "/OrderSuccess/Index";
            }

            // CHI CHO PHEP CHUYEN DEN DUONG DAN NOI BO
            if (
                typeof redirectUrl === "string" &&
                redirectUrl.startsWith("/") &&
                !redirectUrl.startsWith("//") &&
                !redirectUrl.startsWith("/\\")
            ) {
                // KHONG HIEN ALERT THANH CONG
                // CHUYEN TRANG NGAY LAP TUC
                window.location.replace(redirectUrl);
                return;
            }

            // DON DA LUU NHUNG KHONG CO LINK HOP LE
            console.error(
                "Đơn đã lưu nhưng thiếu redirectUrl:",
                result
            );

            alert(
                "Đơn hàng #" + result.orderId +
                " đã được lưu nhưng không mở được trang tiếp theo. " +
                "Vui lòng liên hệ cửa hàng, không đặt lại đơn."
            );

        } catch (error) {
            console.error("Lỗi đặt hàng:", error);

            if (orderSaved) {
                alert(
                    "Đơn hàng đã được lưu vào SQL Server. " +
                    "Có lỗi khi chuyển trang. " +
                    "Vui lòng không đặt lại đơn hàng."
                );
            } else {
                alert(
                    "Không thể hoàn tất yêu cầu đặt hàng.\n\n" +
                    error.message +
                    "\n\nNếu mạng bị gián đoạn, hãy kiểm tra " +
                    "đơn hàng trước khi thử lại."
                );
            }

        } finally {
            submittingOrder = false;

            if (submitButton) {
                submitButton.disabled = false;
                submitButton.innerHTML =
                    '<i class="fa-solid fa-check"></i> ' +
                    'XÁC NHẬN ĐẶT HÀNG';
            }
        }
    }

    Object.assign(window, {
        filterCategory: filterCategory,
        applySearch: applySearch,
        searchProducts: searchProducts,
        chooseKeyword: chooseKeyword,
        resetSearch: resetSearch,
        showMore: showMore,
        closeModal: closeModal,
        hideModal: hideModal,
        showDetail: showDetail,
        addToCart: addToCart,
        buyNow: buyNow,
        changeQuantity: changeQuantity,
        removeFromCart: removeFromCart,
        showCart: showCart,
        goCheckout: goCheckout,
        backToCart: backToCart,
        submitOrder: submitOrder
    });

    function initialize() {
        const searchInput = document.getElementById("searchInput");
        const searchWrap = document.getElementById("searchWrap");
        const searchPanel = document.getElementById("searchPanel");

        if (searchInput && searchPanel) {
            searchInput.addEventListener("focus", function () {
                searchPanel.classList.add("open");
            });
        }

        if (searchWrap && searchPanel) {
            document.addEventListener("click", function (event) {
                if (!searchWrap.contains(event.target)) {
                    searchPanel.classList.remove("open");
                }
            });
        }

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                if (searchPanel) {
                    searchPanel.classList.remove("open");
                }

                document.querySelectorAll(".modal")
                    .forEach(function (modal) {
                        modal.style.display = "none";
                    });

                document.body.classList.remove("modal-open");
            }
        });

        loadProducts();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }

})();
