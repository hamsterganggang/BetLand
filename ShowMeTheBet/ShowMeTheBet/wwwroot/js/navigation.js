window.navigateToHome = function() {
    window.location.href = '/';
};

window.navigateToGame = function() {
    // 여러 방법으로 페이지 이동 시도
    try {
        window.location.replace('/game');
    } catch (e) {
        try {
            window.location.href = '/game';
        } catch (e2) {
            try {
                window.location = '/game';
            } catch (e3) {
                // 최후의 수단
                document.location.href = '/game';
            }
        }
    }
};

window.forceNavigate = function(url) {
    window.location.replace(url);
};

