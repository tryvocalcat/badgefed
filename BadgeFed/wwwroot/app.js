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