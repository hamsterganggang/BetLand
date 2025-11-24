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

window.logout = async function() {
    console.log('logout 함수 호출됨');
    try {
        console.log('API 호출 시도: /api/auth/logout');
        const response = await fetch('/api/auth/logout', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        console.log('API 응답 받음:', response.status, response.statusText);
        
        // 응답과 관계없이 로그인 페이지로 이동
        console.log('로그인 페이지로 이동 시도');
        window.location.href = '/login';
    } catch (error) {
        console.error('로그아웃 오류:', error);
        // 오류 발생 시에도 로그인 페이지로 이동
        console.log('오류 발생, 로그인 페이지로 이동 시도');
        window.location.href = '/login';
    }
};

window.checkAuth = async function() {
    try {
        const response = await fetch('/api/auth/check', {
            method: 'GET',
            credentials: 'include',
            cache: 'no-cache'
        });
        
        if (response.ok) {
            const data = await response.json();
            console.log('인증 확인 결과:', data);
            return data;
        } else {
            console.error('인증 확인 실패:', response.status);
            return { success: false, authenticated: false };
        }
    } catch (error) {
        console.error('인증 확인 오류:', error);
        return { success: false, authenticated: false };
    }
};

window.getCookie = function(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
};

