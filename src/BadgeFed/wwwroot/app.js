window.scrollToElement = function (elementId)
{
    console.log("Scrolling into", elementId);
    if (document.getElementById(elementId) == null) {
        return;
    }
    document.getElementById(elementId).scrollIntoView();
    document.getElementById(elementId).focus();
};


window.showToast = function()
{
    var toastEl = document.getElementById('liveToast');
    var toast = new bootstrap.Toast(toastEl);
    toast.show();
};

// BadgeFed Embed Modal utilities
window.badgeFedUtils = {
    selectText: function(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.select();
            element.setSelectionRange(0, 99999); // For mobile devices
        }
    },
    
    copyToClipboard: function(text) {
        if (navigator.clipboard) {
            return navigator.clipboard.writeText(text);
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
            return Promise.resolve();
        }
    }
};

window.setCookie = function(name, value, days) {
    let expires = "";
    if (days) {
        let date = new Date();
        date.setTime(date.getTime() + (days*24*60*60*1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/";
};

;(function () {
    function findMenu(toggle) {
        const menuId = toggle.getAttribute('aria-controls');
        const navbar = toggle.closest('.navbar');

        if (menuId) {
            return (navbar && navbar.querySelector('#' + menuId)) || document.getElementById(menuId);
        }

        return navbar ? navbar.querySelector('[data-menu]') : null;
    }

    function setMenuState(toggle, menu, isOpen) {
        toggle.classList.toggle('is-active', isOpen);
        toggle.setAttribute('aria-expanded', String(isOpen));

        if (menu) {
            menu.classList.toggle('is-active', isOpen);
        }
    }

    function closeNavbar(navbar) {
        if (!navbar) {
            return;
        }

        const toggle = navbar.querySelector('[data-menu-toggle]');
        const menu = navbar.querySelector('[data-menu]');

        if (toggle && menu) {
            setMenuState(toggle, menu, false);
        }
    }

    function closeAllNavbars() {
        document.querySelectorAll('.navbar').forEach(closeNavbar);
    }

    document.addEventListener('click', function (event) {
        const toggle = event.target.closest('[data-menu-toggle]');
        if (toggle) {
            const menu = findMenu(toggle);
            const isOpen = !toggle.classList.contains('is-active');

            closeAllNavbars();
            setMenuState(toggle, menu, isOpen);
            return;
        }

        if (window.innerWidth <= 1023) {
            const navItem = event.target.closest('.navbar-menu.is-active a.navbar-item');
            if (navItem) {
                closeNavbar(navItem.closest('.navbar'));
                return;
            }
        }

        if (!event.target.closest('.navbar')) {
            closeAllNavbars();
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeAllNavbars();
        }
    });

    window.addEventListener('resize', function () {
        if (window.innerWidth >= 1024) {
            closeAllNavbars();
        }
    });
})();
