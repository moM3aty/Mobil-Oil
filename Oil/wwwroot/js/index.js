function updateCartBadge() {
    const cart = JSON.parse(localStorage.getItem('cart')) || [];
    const totalQuantity = cart.reduce((sum, p) => sum + (p.quantity || 0), 0);
    const badge = document.querySelector('.fixed-cart-button .badge');
    if (badge) {
        badge.textContent = totalQuantity;
        badge.style.display = totalQuantity > 0 ? 'inline-block' : 'none';
    }
}

window.addEventListener('load', function () {
    if (window.location.hash && window.location.hash !== "#home") { // Avoid clearing #home on initial load
        // Check if the hash corresponds to an actual element ID before scrolling
        try {
            const elementToScroll = document.querySelector(window.location.hash);
            if (!elementToScroll) { // If no element, or it's just #, clear it.
                history.replaceState(null, null, ' ');
                window.scrollTo(0, 0); // Optional: scroll to top if hash was invalid/cleared
            } else {
                // If it's a valid section, scrolling will be handled by browser or smooth scroll polyfill
            }
        } catch (e) { // Invalid selector
            history.replaceState(null, null, ' ');
            window.scrollTo(0, 0);
        }
    }
});


document.addEventListener("DOMContentLoaded", () => {
    const currentLangDirection = document.documentElement.dir || "rtl";

    // --- Scroll to Top Logic ---
    const scrollToTopBtn = document.querySelector("#scrollToTopBtn");
    if (scrollToTopBtn) {
        const toggleScrollToTopButton = () => {
            if (document.body.scrollTop > 100 || document.documentElement.scrollTop > 100) {
                scrollToTopBtn.style.display = "block";
            } else {
                scrollToTopBtn.style.display = "none";
            }
        };
        toggleScrollToTopButton(); // Initial check
        window.addEventListener('scroll', toggleScrollToTopButton); // Use addEventListener for multiple scroll handlers
        scrollToTopBtn.addEventListener('click', () => {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    // --- Language Switcher & Swiper Logic ---
    const langSelect = document.getElementById('language-select');
    const htmlElement = document.documentElement; // Use documentElement directly
    const sliderElement = document.querySelector(".mySwiper"); // For offers slider
    let swiperInstance;

    function updateElementText(element, lang) {
        const text = element.getAttribute(`data-${lang}`);
        if (text !== null) { // Check for null to allow empty strings if intended
            element.textContent = text;
        }
    }

    function updateAllButtonTexts(lang) {
        document.querySelectorAll('.product-card-offer .add-to-cart, .product-card .add-to-cart').forEach(btn => updateElementText(btn, lang));
        document.querySelectorAll('.product-card-offer .confirm-add, .product-card .confirm-add').forEach(btn => updateElementText(btn, lang));
        document.querySelectorAll('.product-card-offer .show-details, .product-card .show-details').forEach(btn => updateElementText(btn, lang));
        // Add more selectors if other buttons need dynamic text update
        document.querySelectorAll('.cart-checkout-btn, .modal-footer .btn, #submit-order-btn, .btn-custom').forEach(btn => {
            if (btn.hasAttribute(`data-${lang}`)) updateElementText(btn, lang);
        });
    }


    function updateFormPlaceholders() {
        const dir = htmlElement.getAttribute('dir');
        const isRTL = dir === 'rtl';
        const currentLang = isRTL ? 'ar' : 'en';

        document.querySelectorAll('input[data-ar-placeholder][data-en-placeholder]').forEach(el => {
            el.placeholder = el.getAttribute(`data-${currentLang}-placeholder`);
        });
        // For contact form (if IDs are stable)
        const nameInput = document.getElementById("UserName");
        const emailInput = document.getElementById("UserEmail");
        const messageInput = document.getElementById("message");
        if (nameInput && nameInput.hasAttribute('data-ar-placeholder')) nameInput.placeholder = isRTL ? nameInput.getAttribute('data-ar-placeholder') : nameInput.getAttribute('data-en-placeholder');
        if (emailInput && emailInput.hasAttribute('data-ar-placeholder')) emailInput.placeholder = isRTL ? emailInput.getAttribute('data-ar-placeholder') : emailInput.getAttribute('data-en-placeholder');
        if (messageInput && messageInput.hasAttribute('data-ar-placeholder')) messageInput.placeholder = isRTL ? messageInput.getAttribute('data-ar-placeholder') : messageInput.getAttribute('data-en-placeholder');

        const submitBtn = document.querySelector('#contactForm button[type="submit"]');
        if (submitBtn && submitBtn.hasAttribute('data-ar')) submitBtn.textContent = isRTL ? submitBtn.getAttribute('data-ar') : submitBtn.getAttribute('data-en');

        const formSuccess = document.getElementById('formSuccess');
        if (formSuccess && formSuccess.hasAttribute('data-ar')) formSuccess.textContent = isRTL ? formSuccess.getAttribute('data-ar') : formSuccess.getAttribute('data-en');
    }

    function initializeSwiper() {
        if (!sliderElement) return;
        const isRTL = htmlElement.getAttribute("dir") === "rtl";

        // Ensure slider itself has dir attribute for Swiper to correctly initialize RTL layout
        sliderElement.setAttribute("dir", isRTL ? "rtl" : "ltr");

        if (swiperInstance && typeof swiperInstance.destroy === 'function') {
            swiperInstance.destroy(true, true);
        }

        swiperInstance = new Swiper(".mySwiper", {
            slidesPerView: 1,
            spaceBetween: 20,
            loop: true,
            grabCursor: true,
            watchOverflow: true, // Important for when slidesPerView >= total slides
            rtl: isRTL,
            autoplay: { delay: 3000, disableOnInteraction: false, pauseOnMouseEnter: true },
            navigation: { nextEl: ".swiper-button-next", prevEl: ".swiper-button-prev" },
            pagination: { el: ".swiper-pagination", clickable: false, renderBullet: () => "" },
            breakpoints: {
                500: { slidesPerView: 2, spaceBetween: 15 },
                768: { slidesPerView: 2, spaceBetween: 20 },
                992: { slidesPerView: 3, spaceBetween: 20 },
                1200: { slidesPerView: 4, spaceBetween: 25 }
            },
            on: {
                init: function () {
                    this.update(); // Ensure it updates after init, especially for RTL
                },
                resize: function () {
                    this.update();
                }
            }
        });
    }

    function setLanguage(lang) {
        htmlElement.setAttribute('lang', lang);
        htmlElement.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');

        document.querySelectorAll('[data-ar][data-en]').forEach(element => {
            updateElementText(element, lang);
        });

        updateFormPlaceholders();
        initializeSwiper(); // Re-initialize Swiper for RTL/LTR changes
        document.body.classList.toggle('rtl', lang === 'ar');
        document.body.classList.toggle('ltr', lang === 'en');
        updateAllButtonTexts(lang); // Ensure all dynamic buttons are updated
    }

    if (langSelect) {
        const savedLang = localStorage.getItem('selectedLang') || 'ar';
        langSelect.value = savedLang;
        setLanguage(savedLang); // Set initial language

        langSelect.addEventListener('change', (event) => {
            const selectedLang = event.target.value;
            localStorage.setItem('selectedLang', selectedLang);
            setLanguage(selectedLang);
            // Optionally, send the new language preference to the server
            // fetch(`/Home/ToggleLanguage?culture=${selectedLang}&returnUrl=${window.location.pathname + window.location.search}`);
        });
    } else {
        setLanguage('ar'); // Default to Arabic if no selector found
    }

    // --- Navbar Scroll & Toggle Logic ---
    const navbar = document.querySelector('.hero-navbar');
    const navbarCollapse = document.getElementById('navbarNav');
    if (navbarCollapse && navbar) {
        navbarCollapse.addEventListener('show.bs.collapse', () => navbar.classList.add('menu-open'));
        navbarCollapse.addEventListener('hide.bs.collapse', () => navbar.classList.remove('menu-open'));
    }
    function handleNavbarState() { if (!navbar) return; navbar.classList.toggle('scrolled', window.scrollY > 50); } // Increased threshold
    window.addEventListener('scroll', handleNavbarState);
    handleNavbarState();

    // --- Contact Form Validation & Mock Submission Logic ---
    const contactForm = document.getElementById("contactForm");
    if (contactForm) {
        const nameInput = document.getElementById("UserName");
        const emailInput = document.getElementById("UserEmail");
        const messageInput = document.getElementById("message");
        const nameError = document.getElementById("nameError");
        const emailError = document.getElementById("emailError");
        const messageError = document.getElementById("messageError");
        const successMessageEl = document.getElementById("formSuccess");

        function validateContactForm() {
            let isValid = true;
            const dir = htmlElement.getAttribute('dir');
            const isRTL = dir === 'rtl';
            const name = nameInput?.value.trim() || '';
            const email = emailInput?.value.trim() || '';
            const message = messageInput?.value.trim() || '';

            if (name.length < 3) { if (nameError) nameError.textContent = isRTL ? "الاسم يجب أن يكون على الأقل 3 أحرف." : "Name must be at least 3 characters."; isValid = false; } else { if (nameError) nameError.textContent = ""; }
            const emailRegex = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
            if (!emailRegex.test(email)) { if (emailError) emailError.textContent = isRTL ? "يرجى إدخال بريد إلكتروني صالح." : "Please enter a valid email."; isValid = false; } else { if (emailError) emailError.textContent = ""; }
            if (message.length < 10) { if (messageError) messageError.textContent = isRTL ? "يجب أن تكون الرسالة 10 أحرف على الأقل." : "Message must be at least 10 characters."; isValid = false; } else { if (messageError) messageError.textContent = ""; }
            return isValid;
        }

        [nameInput, emailInput, messageInput].forEach(input => { if (input) input.addEventListener("input", validateContactForm); });

        contactForm.addEventListener("submit", function (e) {
            e.preventDefault();
            const isRTL = htmlElement.getAttribute('dir') === 'rtl';
            if (validateContactForm()) {
                // Mock submission - In a real app, you'd send this via fetch to a server endpoint
                const formData = { name: nameInput.value.trim(), email: emailInput.value.trim(), message: messageInput.value.trim() };
                console.log("Contact form data (mock submission):", formData);
                localStorage.setItem("contactFormData", JSON.stringify(formData));
                if (successMessageEl) {
                    successMessageEl.textContent = isRTL ? "تم إرسال الرسالة بنجاح." : "Message sent successfully.";
                    successMessageEl.classList.remove("d-none");
                }
                contactForm.reset();
                setTimeout(() => { if (successMessageEl) successMessageEl.classList.add("d-none"); }, 3000);
            } else {
                if (successMessageEl) successMessageEl.classList.add("d-none");
            }
        });
    }

    // --- Offers Cart Logic (for .product-card-offer elements, typically on Index.cshtml) ---
    const offerCards = document.querySelectorAll(".product-card-offer");
    if (offerCards.length > 0) {
        offerCards.forEach(card => {
            const addToCartBtn = card.querySelector(".add-to-cart");
            const quantitySection = card.querySelector(".quantity-section");
            const confirmAddBtn = card.querySelector(".confirm-add");
            const quantityInput = card.querySelector(".quantity-input");

            addToCartBtn?.addEventListener("click", () => {
                if (quantitySection) quantitySection.classList.remove("d-none");
                if (addToCartBtn) addToCartBtn.classList.add("d-none");
            });

            confirmAddBtn?.addEventListener("click", () => {
                const cardLangDir = htmlElement.dir || "rtl";
                const productIdString = card.getAttribute("data-product-id");
                const productId = parseInt(productIdString);
                const titleAr = card.getAttribute("data-title-ar") || "";
                const titleEn = card.getAttribute("data-title-en") || "";
                let productTitleForCart = (cardLangDir === 'rtl' ? titleAr : titleEn).trim();
                if (!productTitleForCart) { productTitleForCart = (titleAr || titleEn || (cardLangDir === 'rtl' ? "عرض" : "Offer")).trim(); }
                const priceString = card.getAttribute("data-price");
                const productPrice = parseFloat(priceString);
                const productImage = card.getAttribute("data-image") || "/images/products/default.png";
                const quantity = parseInt(quantityInput.value);

                if (isNaN(productId) || productId <= 0) { console.error("Invalid productId for offer:", productIdString); Swal.fire({ icon: 'error', title: (cardLangDir === 'rtl' ? 'خطأ' : 'Error'), text: (cardLangDir === 'rtl' ? 'معرف المنتج غير صالح.' : 'Invalid product ID.') }); return; }
                if (isNaN(quantity) || quantity < 1) { Swal.fire({ icon: 'error', title: (cardLangDir === 'rtl' ? 'خطأ' : 'Error'), text: (cardLangDir === 'rtl' ? 'كمية غير صالحة.' : 'Invalid quantity.') }); return; }
                if (isNaN(productPrice) || productPrice < 0) { Swal.fire({ icon: 'error', title: (cardLangDir === 'rtl' ? 'خطأ' : 'Error'), text: (cardLangDir === 'rtl' ? 'سعر غير صالح.' : 'Invalid price.') }); return; }

                const productForCart = { productId: productId, title: productTitleForCart, price: productPrice, imageUrl: productImage, quantity: quantity };
                let cart = JSON.parse(localStorage.getItem("cart")) || [];
                const existingProductIndex = cart.findIndex(p => p.productId === productForCart.productId);
                if (existingProductIndex > -1) { cart[existingProductIndex].quantity += productForCart.quantity; } else { cart.push(productForCart); }
                localStorage.setItem("cart", JSON.stringify(cart));
                if (quantitySection) quantitySection.classList.add("d-none");
                if (addToCartBtn) addToCartBtn.classList.remove("d-none");
                if (quantityInput) quantityInput.value = 1;
                updateCartBadge();
                Swal.fire({
                    icon: 'success', title: (cardLangDir === 'rtl' ? 'أُضيف للسلة!' : 'Added to Cart!'), timer: 2500, 
                    timerProgressBar: true,  showConfirmButton: false, toast: true, position: 'top-end' });
            });
        });
    }

    // --- AOS Initialization ---
    if (typeof AOS !== 'undefined') { // Check if AOS is loaded
        AOS.init({ offset: 100, duration: 800, easing: 'ease-in-out-quad', once: true });
    }

    // Initial calls
    updateCartBadge();
    // initializeSwiper(); // Already called within setLanguage, which is called on DOMContentLoaded
});
