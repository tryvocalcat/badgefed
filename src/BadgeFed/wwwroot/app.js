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
}
