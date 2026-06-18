// ==================================================================
// RED WOLF - Полный клиентский код
// ==================================================================

// ===== СОСТОЯНИЕ =====
const state = {
    user: null,
    token: null,
    chat: null,
    chatInfo: null,
    privateKey: null,
    sessionKeys: new Map(),
    onlineUsers: new Map(),
    unread: new Map(),
    connection: null,
    editingMessage: null,
    editingIsEncrypted: false,
    selectedMembers: [],
    tempPhone: null,
    serverPublicKey: null,
    groupAvatarCache: new Map(),
};

const API = '/api';
const DEFAULT_AVATAR = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%236b7280'%3E%3Cpath d='M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z'/%3E%3C/svg%3E";

// ===== УТИЛИТЫ =====
function getSafeToken() { return encodeURIComponent(state.token || ''); }
function escapeHtml(str) { if (!str) return ''; return str.replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[m]); }

function toast(msg, isErr = false) {
    const el = document.createElement('div');
    el.className = 'toast';
    el.textContent = msg;
    if (isErr) el.style.borderColor = '#e84c4c';
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 3000);
}

function closeModals() { document.querySelectorAll('.modal').forEach(m => m.classList.remove('active')); }
function showModal(id) { closeModals(); document.getElementById(id).classList.add('active'); }

function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    if (isNaN(d)) return '';
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const msgDate = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const diff = Math.floor((today - msgDate) / (1000 * 60 * 60 * 24));
    const time = d.toTimeString().slice(0, 5);
    if (diff === 0) return time;
    if (diff === 1) return `Вчера ${time}`;
    if (diff < 7) return `${['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'][d.getDay()]} ${time}`;
    return `${d.getDate()}.${d.getMonth() + 1}.${d.getFullYear()} ${time}`;
}

function getFileIcon(name) {
    const ext = name?.split('.').pop()?.toLowerCase();
    if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(ext)) return '🖼️';
    if (['mp4', 'mov', 'avi', 'mkv'].includes(ext)) return '🎥';
    if (['pdf'].includes(ext)) return '📕';
    if (['doc', 'docx'].includes(ext)) return '📘';
    if (['xls', 'xlsx'].includes(ext)) return '📊';
    return '📎';
}

function formatFileSize(bytes) {
    if (!bytes) return '0 B';
    const k = 1024, sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return (bytes / Math.pow(k, i)).toFixed(1) + ' ' + sizes[i];
}

function isEdited(msg) {
    if (!msg.messageLastUpdateDate || !msg.messageCreateDate) return false;
    return Math.abs(new Date(msg.messageLastUpdateDate) - new Date(msg.messageCreateDate)) > 1000;
}

function copyToClipboard(text) { navigator.clipboard.writeText(text); toast('📋 Скопировано!'); }

// ===== API =====
function headers() {
    return {
        'Content-Type': 'application/json',
        ...(state.token ? { 'Authorization': `Bearer ${state.token}` } : {})
    };
}

async function apiCall(url, opts = {}) {
    const res = await fetch(API + url, { headers: headers(), ...opts });
    if (!res.ok) throw new Error(await res.text() || `HTTP ${res.status}`);
    return res;
}

// ===== КРИПТОГРАФИЯ =====
async function generateRSAKeys() {
    return await crypto.subtle.generateKey(
        { name: "RSA-OAEP", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" },
        true, ["encrypt", "decrypt"]
    );
}

async function exportPublicKey(key) {
    const exported = await crypto.subtle.exportKey("spki", key);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function exportPrivateKey(key) {
    const exported = await crypto.subtle.exportKey("pkcs8", key);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function importPublicKey(b64) {
    const clean = b64?.trim()?.replace(/\s/g, '') || '';
    const binary = Uint8Array.from(atob(clean), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("spki", binary, { name: "RSA-OAEP", hash: "SHA-256" }, false, ["encrypt"]);
}

async function importPrivateKey(b64) {
    const clean = b64?.trim()?.replace(/\s/g, '') || '';
    const binary = Uint8Array.from(atob(clean), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("pkcs8", binary, { name: "RSA-OAEP", hash: "SHA-256" }, false, ["decrypt"]);
}

async function encryptWithPublicKey(data, key) {
    const encrypted = await crypto.subtle.encrypt({ name: "RSA-OAEP" }, key, data);
    return btoa(String.fromCharCode(...new Uint8Array(encrypted)));
}

async function generateSessionKey() {
    return await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
}

async function exportSessionKey(key) {
    const exported = await crypto.subtle.exportKey("raw", key);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function importSessionKey(b64) {
    const binary = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("raw", binary, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);
}

async function encryptForUsers(text, sessionKey) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const encoded = new TextEncoder().encode(text);
    const encrypted = await crypto.subtle.encrypt({ name: "AES-GCM", iv: iv }, sessionKey, encoded);
    return {
        encryptedData: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
        iv: btoa(String.fromCharCode(...iv))
    };
}

async function decryptMessage(encryptedB64, ivB64, sessionKey) {
    const encrypted = Uint8Array.from(atob(encryptedB64), c => c.charCodeAt(0));
    const iv = Uint8Array.from(atob(ivB64), c => c.charCodeAt(0));
    const decrypted = await crypto.subtle.decrypt({ name: "AES-GCM", iv: iv }, sessionKey, encrypted);
    return new TextDecoder().decode(decrypted);
}

async function encryptForServer(text, serverPublicKey) {
    const aesKey = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const encoded = new TextEncoder().encode(text);
    const encryptedMsg = await crypto.subtle.encrypt({ name: "AES-GCM", iv: iv }, aesKey, encoded);
    const aesKeyRaw = await crypto.subtle.exportKey("raw", aesKey);
    const encryptedAesKey = await crypto.subtle.encrypt({ name: "RSA-OAEP" }, serverPublicKey, aesKeyRaw);
    const combined = new Uint8Array(encryptedAesKey.byteLength + encryptedMsg.byteLength);
    combined.set(new Uint8Array(encryptedAesKey), 0);
    combined.set(new Uint8Array(encryptedMsg), encryptedAesKey.byteLength);
    return {
        encryptedData: btoa(String.fromCharCode(...combined)),
        iv: btoa(String.fromCharCode(...iv))
    };
}

async function loadServerPublicKey() {
    try {
        const res = await fetch(`${API}/User/server-public-key`);
        if (res.ok) {
            const data = await res.json();
            if (data.publicKey) {
                state.serverPublicKey = await importPublicKey(data.publicKey);
                console.log('✅ Server public key loaded');
                return true;
            }
        }
        return false;
    } catch (e) {
        console.error('Failed to load server key:', e);
        return false;
    }
}

// ===== INDEXEDDB =====
const DB_NAME = "RedWolfKeys";
const DB_VERSION = 2;

function openDB() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onerror = () => reject(req.error);
        req.onsuccess = () => resolve(req.result);
        req.onupgradeneeded = (e) => {
            const db = e.target.result;
            if (!db.objectStoreNames.contains("keys")) {
                db.createObjectStore("keys", { keyPath: "id" });
            }
        };
    });
}

async function savePrivateKey(b64) {
    const db = await openDB();
    const tx = db.transaction("keys", "readwrite");
    tx.objectStore("keys").put({ id: "privateKey", value: b64 });
    return new Promise((resolve, reject) => { tx.oncomplete = resolve; tx.onerror = reject; });
}

async function loadPrivateKey() {
    const db = await openDB();
    const tx = db.transaction("keys", "readonly");
    const req = tx.objectStore("keys").get("privateKey");
    return new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result?.value || null);
        req.onerror = reject;
    });
}

async function saveSessionKey(chatId, b64) {
    const db = await openDB();
    const tx = db.transaction("keys", "readwrite");
    tx.objectStore("keys").put({ id: `sessionKey_${chatId}`, value: b64 });
    return new Promise((resolve, reject) => { tx.oncomplete = resolve; tx.onerror = reject; });
}

async function loadAllSessionKeys() {
    const db = await openDB();
    const tx = db.transaction("keys", "readonly");
    const req = tx.objectStore("keys").getAll();
    const items = await new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result);
        req.onerror = reject;
    });
    for (const item of items) {
        if (item.id?.startsWith('sessionKey_')) {
            const chatId = item.id.replace('sessionKey_', '');
            try {
                const key = await importSessionKey(item.value);
                state.sessionKeys.set(chatId, key);
            } catch (e) { /* ignore */ }
        }
    }
}

async function getSessionKeyFromDB(chatId) {
    const db = await openDB();
    const tx = db.transaction("keys", "readonly");
    const req = tx.objectStore("keys").get(`sessionKey_${chatId}`);
    return new Promise((resolve, reject) => {
        req.onsuccess = async () => {
            if (req.result?.value) {
                try {
                    resolve(await importSessionKey(req.result.value));
                } catch (e) { resolve(null); }
            } else resolve(null);
        };
        req.onerror = reject;
    });
}

// ===== АВТОРИЗАЦИЯ =====
async function handleLogin() {
    const login = document.getElementById('loginLogin').value;
    const pwd = document.getElementById('loginPassword').value;
    if (!login || !pwd) return toast('Заполните все поля', true);
    try {
        const res = await fetch(API + '/User/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ login, password: pwd })
        });
        if (!res.ok) throw new Error(await res.text());
        const data = await res.json();
        state.token = data.token;
        state.user = { id: data.userId };
        localStorage.setItem('token', state.token);
        localStorage.setItem('userId', data.userId);

        const profileRes = await apiCall('/User/profile');
        const profile = await profileRes.json();
        state.user.name = profile.name;
        state.user.publicKey = profile.publicKey;

        const privKeyB64 = await loadPrivateKey();
        if (privKeyB64) {
            state.privateKey = await importPrivateKey(privKeyB64);
        }

        await loadAllSessionKeys();
        await initApp();
    } catch (e) {
        toast('Ошибка входа: ' + e.message, true);
    }
}

async function requestCode() {
    const phone = document.getElementById('regPhone').value.trim();
    const name = document.getElementById('regName').value.trim();
    const pwd = document.getElementById('regPassword').value;
    if (!phone || !name || !pwd) return toast('Заполните все поля', true);
    if (pwd.length < 6) return toast('Пароль минимум 6 символов', true);
    try {
        const res = await fetch(API + '/User/request-verification', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: phone, name, password: pwd })
        });
        if (!res.ok) throw new Error(await res.text());
        const data = await res.json();
        state.tempPhone = data.phoneNumber;
        document.getElementById('registerForm').style.display = 'none';
        document.getElementById('verifyForm').style.display = 'flex';
        document.getElementById('verifyCode').value = data.code || '';
        toast('✅ Код отправлен на ' + phone);
    } catch (e) {
        toast(e.message, true);
    }
}

async function verifyRegistration() {
    const code = document.getElementById('verifyCode').value.trim();
    if (!code || code.length !== 6) return toast('Введите 6-значный код', true);
    try {
        const res = await fetch(API + '/User/verify-and-register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: state.tempPhone, code })
        });
        if (!res.ok) throw new Error(await res.text());
        const data = await res.json();
        state.token = data.token;
        state.user = { id: data.userId };
        localStorage.setItem('token', state.token);
        localStorage.setItem('userId', data.userId);

        const keyPair = await generateRSAKeys();
        const pubB64 = await exportPublicKey(keyPair.publicKey);
        const privB64 = await exportPrivateKey(keyPair.privateKey);

        await fetch(API + '/User/public-key', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${state.token}` },
            body: JSON.stringify({ publicKey: pubB64 })
        });

        await savePrivateKey(privB64);
        state.privateKey = keyPair.privateKey;
        state.user.publicKey = pubB64;

        await initApp();
    } catch (e) {
        toast(e.message, true);
    }
}

function switchTab(tab) {
    document.querySelectorAll('.auth-tab').forEach(t => t.classList.remove('active'));
    if (tab === 'login') {
        document.querySelector('.auth-tab[data-tab="login"]').classList.add('active');
        document.getElementById('loginForm').style.display = 'flex';
        document.getElementById('registerForm').style.display = 'none';
        document.getElementById('verifyForm').style.display = 'none';
    } else {
        document.querySelector('.auth-tab[data-tab="register"]').classList.add('active');
        document.getElementById('loginForm').style.display = 'none';
        document.getElementById('registerForm').style.display = 'flex';
        document.getElementById('verifyForm').style.display = 'none';
    }
}

// ===== ПРОФИЛЬ =====
async function showProfileModal() {
    try {
        const res = await apiCall('/User/profile');
        const user = await res.json();
        document.getElementById('profileName').value = user.name;
        document.getElementById('profileNewPassword').value = '';
        const avatarUrl = `${API}/User/avatar/${user.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
        document.getElementById('profileAvatar').src = avatarUrl;
        showModal('profileModal');
    } catch (e) {
        toast('Ошибка загрузки профиля', true);
    }
}

async function updateProfile() {
    const name = document.getElementById('profileName').value.trim();
    const pwd = document.getElementById('profileNewPassword').value;
    const data = {};
    if (name) data.name = name;
    if (pwd) data.newPassword = pwd;
    if (Object.keys(data).length === 0) return closeModals();
    try {
        const res = await apiCall('/User/update-profile', { method: 'PUT', body: JSON.stringify(data) });
        if (res.ok) {
            const updated = await res.json();
            if (state.user) state.user.name = updated.name;
            document.getElementById('currentUserName').innerText = updated.name;
            toast('✅ Профиль обновлён');
            closeModals();
            await loadChats();
        }
    } catch (e) {
        toast('Ошибка обновления', true);
    }
}

async function uploadAvatar(file) {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) return toast('Максимум 5MB', true);
    const fd = new FormData();
    fd.append('file', file);
    try {
        const res = await fetch(`${API}/User/upload-avatar`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${state.token}` },
            body: fd
        });
        if (res.ok) {
            const url = `${API}/User/avatar/${state.user.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
            document.getElementById('sidebarAvatar').src = url;
            document.getElementById('profileAvatar').src = url;
            await loadChats();
            toast('✅ Аватар обновлён');
        }
    } catch (e) {
        toast('Ошибка загрузки', true);
    }
}

async function deleteAvatar() {
    if (!confirm('Удалить аватар?')) return;
    try {
        const res = await fetch(`${API}/User/avatar`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (res.ok) {
            document.getElementById('sidebarAvatar').src = DEFAULT_AVATAR;
            document.getElementById('profileAvatar').src = DEFAULT_AVATAR;
            await loadChats();
            toast('✅ Аватар удалён');
        }
    } catch (e) {
        toast('Ошибка удаления', true);
    }
}

// ===== ГРУППОВЫЕ АВАТАРЫ =====
async function getGroupAvatarUrl(chatId) {
    if (!state.token || !chatId) return DEFAULT_AVATAR;
    if (state.groupAvatarCache.has(chatId)) {
        const cached = state.groupAvatarCache.get(chatId);
        if (cached.expiry > Date.now()) return cached.url;
    }
    try {
        const res = await fetch(`${API}/Chat/${chatId}/avatar`, {
            headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (res.ok) {
            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            state.groupAvatarCache.set(chatId, { url, expiry: Date.now() + 5 * 60 * 1000 });
            return url;
        }
        return DEFAULT_AVATAR;
    } catch (e) {
        return DEFAULT_AVATAR;
    }
}

async function uploadGroupAvatar(file) {
    if (!file || !state.chat) return;
    if (file.size > 5 * 1024 * 1024) return toast('Максимум 5MB', true);
    const fd = new FormData();
    fd.append('file', file);
    try {
        const res = await fetch(`${API}/Chat/${state.chat.id}/avatar`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${state.token}` },
            body: fd
        });
        if (res.ok) {
            state.groupAvatarCache.delete(state.chat.id);
            toast('✅ Аватар группы обновлён');
            await loadChats();
            const url = await getGroupAvatarUrl(state.chat.id);
            document.getElementById('groupAvatarPreview').src = url;
        }
    } catch (e) {
        toast('Ошибка загрузки', true);
    }
}

async function deleteGroupAvatar() {
    if (!confirm('Удалить аватар группы?')) return;
    try {
        const res = await fetch(`${API}/Chat/${state.chat.id}/avatar`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (res.ok) {
            state.groupAvatarCache.delete(state.chat.id);
            document.getElementById('groupAvatarPreview').src = DEFAULT_AVATAR;
            await loadChats();
            toast('✅ Аватар группы удалён');
        }
    } catch (e) {
        toast('Ошибка удаления', true);
    }
}

// ===== НЕПРОЧИТАННЫЕ =====
async function loadUnreadCounts() {
    try {
        const res = await apiCall('/Message/unread');
        const counts = await res.json();
        state.unread.clear();
        for (const [k, v] of Object.entries(counts)) state.unread.set(k, v);
        updateAllUnread();
    } catch (e) { console.error(e); }
}

function updateAllUnread() {
    document.querySelectorAll('.chat-item').forEach(item => {
        const chatId = item.dataset.id;
        const count = state.unread.get(chatId) || 0;
        const nameDiv = item.querySelector('.name');
        const existing = nameDiv?.querySelector('.unread');
        if (count > 0) {
            if (existing) existing.textContent = count > 99 ? '99+' : count;
            else if (nameDiv) {
                const badge = document.createElement('span');
                badge.className = 'unread';
                badge.textContent = count > 99 ? '99+' : count;
                nameDiv.appendChild(badge);
            }
        } else if (existing) existing.remove();
    });
}

async function markChatAsRead(chatId) {
    try {
        await apiCall(`/Message/${chatId}/mark-read`, { method: 'POST' });
        state.unread.delete(chatId);
        updateAllUnread();
    } catch (e) { console.error(e); }
}

// ===== ЧАТЫ =====
async function getChatInfo(chatId) {
    try {
        const res = await apiCall('/Chat/' + chatId);
        return await res.json();
    } catch (e) { return null; }
}

async function getSessionKeyForChat(chatId) {
    if (state.sessionKeys.has(chatId)) return state.sessionKeys.get(chatId);
    try {
        const dbKey = await getSessionKeyFromDB(chatId);
        if (dbKey) {
            state.sessionKeys.set(chatId, dbKey);
            return dbKey;
        }
    } catch (e) { /* ignore */ }

    if (!state.privateKey) {
        toast('❌ Ключ шифрования не загружен', true);
        return null;
    }

    try {
        const res = await fetch(`${API}/Chat/${chatId}/session-key`, {
            headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (!res.ok) return null;
        const data = await res.json();
        if (!data.encryptedKey) return null;
        const encrypted = Uint8Array.from(atob(data.encryptedKey), c => c.charCodeAt(0));
        const decrypted = await crypto.subtle.decrypt({ name: "RSA-OAEP" }, state.privateKey, encrypted);
        const key = await crypto.subtle.importKey("raw", decrypted, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);
        state.sessionKeys.set(chatId, key);
        const b64 = btoa(String.fromCharCode(...new Uint8Array(decrypted)));
        await saveSessionKey(chatId, b64);
        return key;
    } catch (e) {
        console.error('Failed to get session key:', e);
        return null;
    }
}

async function createPrivateChat(uid, uname) {
    try {
        const pubRes = await apiCall(`/User/public-key/${uid}`);
        const pubData = await pubRes.json();
        if (!pubData.publicKey) {
            toast(`У пользователя ${uname} нет ключа шифрования`, true);
            return;
        }
        const otherKey = await importPublicKey(pubData.publicKey);
        const sessionKey = await generateSessionKey();
        const rawKey = await exportSessionKey(sessionKey);
        const myKey = await importPublicKey(state.user.publicKey);
        const encForMe = await encryptWithPublicKey(Uint8Array.from(atob(rawKey), c => c.charCodeAt(0)), myKey);
        const encForOther = await encryptWithPublicKey(Uint8Array.from(atob(rawKey), c => c.charCodeAt(0)), otherKey);

        const chatRes = await apiCall('/Chat', {
            method: 'POST',
            body: JSON.stringify({ memberIds: [state.user.id, uid], maxUsers: 2 })
        });
        const chat = await chatRes.json();
        await apiCall(`/Chat/${chat.id}/session-keys`, {
            method: 'POST',
            body: JSON.stringify({
                encryptedKeys: { [state.user.id]: encForMe, [uid]: encForOther }
            })
        });

        state.sessionKeys.set(chat.id, sessionKey);
        await saveSessionKey(chat.id, rawKey);
        closeModals();
        await loadChats();
        await selectChat(chat.id, chat.chatName);
        toast(`✨ Чат с ${uname} создан`);
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

async function loadLastMessage(chat) {
    try {
        const res = await apiCall(`/Chat/${chat.id}/messages?page=1&pageSize=1`);
        const data = await res.json();
        if (data.messages?.length > 0) {
            const msg = data.messages[0];
            const key = state.sessionKeys.get(chat.id) || await getSessionKeyFromDB(chat.id);
            if (msg.encryptedData && msg.iv && key) {
                try {
                    msg.messageText = await decryptMessage(msg.encryptedData, msg.iv, key);
                } catch (e) { /* keep as is */ }
            }
            return msg;
        }
        return null;
    } catch (e) { return null; }
}

async function loadChats() {
    try {
        if (!state.token) return;
        const [chatsRes, unreadRes] = await Promise.all([
            apiCall(`/Chat/user-chats/${state.user.id}`),
            apiCall('/Message/unread')
        ]);
        const chats = await chatsRes.json();
        const unreadData = await unreadRes.json();
        state.unread.clear();
        for (const [k, v] of Object.entries(unreadData)) state.unread.set(k, v);

        const container = document.getElementById('chatsList');
        if (!chats?.length) {
            container.innerHTML = '<div class="empty-state">💬 Нет чатов<br>Начните новый диалог</div>';
            return;
        }

        container.innerHTML = '';
        for (const chat of chats) {
            const isGroup = chat.maxUsers > 2 || (chat.users?.length > 2);
            const name = isGroup ? chat.chatName : (chat.otherUser?.name || chat.chatName);
            const online = !isGroup && chat.otherUser?.id ? state.onlineUsers.get(chat.otherUser.id) === true : false;
            const unread = state.unread.get(chat.id) || 0;

            let avatarUrl = DEFAULT_AVATAR;
            if (isGroup) {
                avatarUrl = await getGroupAvatarUrl(chat.id);
            } else if (chat.otherUser?.id) {
                avatarUrl = `${API}/User/avatar/${chat.otherUser.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
            }

            const lastMsg = await loadLastMessage(chat);
            let preview = '';
            if (lastMsg) {
                const isOwn = lastMsg.userId === state.user?.id;
                const sender = isOwn ? 'Вы' : (lastMsg.user?.name || lastMsg.messageCreator?.name || 'Unknown');
                let text = lastMsg.messageText || (lastMsg.fileName ? '📎 Файл' : '');
                if (!text && lastMsg.encryptedData) text = '🔒 Зашифровано';
                if (text?.length > 50) text = text.slice(0, 47) + '...';
                preview = `<div class="preview"><strong>${escapeHtml(sender)}:</strong> ${escapeHtml(text)}</div>`;
            }

            const html = `
                <div class="chat-item" data-id="${chat.id}">
                    <img class="avatar" src="${avatarUrl}" onerror="this.src='${DEFAULT_AVATAR}'">
                    <div class="info">
                        <div class="name">${escapeHtml(name)}${isGroup ? ' <span class="badge">Группа</span>' : ''}</div>
                        ${preview}
                    </div>
                    ${unread > 0 ? `<span class="unread">${unread > 99 ? '99+' : unread}</span>` : ''}
                    ${!isGroup ? `<div class="online-dot ${online ? 'online' : 'offline'}"></div>` : ''}
                </div>
            `;
            container.insertAdjacentHTML('beforeend', html);
        }

        container.querySelectorAll('.chat-item').forEach(item => {
            item.addEventListener('click', () => {
                const id = item.dataset.id;
                const name = item.querySelector('.name')?.textContent?.trim() || 'Чат';
                selectChat(id, name);
            });
        });

    } catch (e) { console.error(e); }
}

// ===== ВЫБОР ЧАТА =====
async function selectChat(chatId, chatName) {
    state.chat = { id: chatId, name: chatName };
    state.chatInfo = await getChatInfo(chatId);
    const isGroup = state.chatInfo && (state.chatInfo.maxUsers > 2 || (state.chatInfo.users?.length > 2));
    const title = isGroup ? state.chatInfo.chatName : (state.chatInfo?.otherUser?.name || chatName);
    const otherUserId = !isGroup && state.chatInfo?.otherUser?.id;

    document.getElementById('chatHeader').innerHTML = `
        <h2${otherUserId ? ' id="chatTitleBtn"' : ''}>${escapeHtml(title)}${isGroup ? ' 👥' : ''}</h2>
        <div class="actions">
            <button class="btn btn-secondary" id="trashChatBtn">🗑️</button>
            <button class="btn btn-secondary" id="editChatBtn">✏️</button>
        </div>
    `;

    if (otherUserId) {
        document.getElementById('chatTitleBtn')?.addEventListener('click', () => showUserProfile(otherUserId, title));
    }
    document.getElementById('editChatBtn')?.addEventListener('click', showEditChatModal);
    document.getElementById('trashChatBtn')?.addEventListener('click', showTrashBin);

    document.getElementById('messageInput').disabled = false;
    document.getElementById('sendBtn').disabled = false;

    const key = await getSessionKeyForChat(chatId);
    if (!key) toast('⚠️ Не удалось загрузить ключ шифрования', true);

    await markChatAsRead(chatId);
    await loadMessages(chatId);
    await loadChats();

    if (state.connection && state.connection.state === signalR.HubConnectionState.Connected) {
        await state.connection.invoke('JoinChat', chatId, state.user.id, state.user.name);
    }

    if (window.innerWidth <= 768) document.getElementById('sidebar').classList.remove('open');
}

// ===== СООБЩЕНИЯ =====
async function loadMessages(chatId) {
    try {
        const res = await apiCall(`/Chat/${chatId}/messages?page=1&pageSize=100`);
        const data = await res.json();
        const key = state.sessionKeys.get(chatId);
        const messages = [];

        for (const msg of (data.messages || [])) {
            if (msg.encryptedData && msg.iv && key) {
                try {
                    const text = await decryptMessage(msg.encryptedData, msg.iv, key);
                    messages.push({ ...msg, messageText: text, decrypted: true });
                } catch (e) {
                    messages.push({ ...msg, messageText: '🔒 [Зашифровано]', decrypted: false });
                }
            } else {
                messages.push(msg);
            }
        }
        renderMessages(messages);

        if (data.messages?.length > 0) {
            const last = data.messages[0];
            if (last.encryptedData && last.iv && key) {
                try {
                    last.messageText = await decryptMessage(last.encryptedData, last.iv, key);
                } catch (e) { /* ignore */ }
            }
        }
    } catch (e) { console.error(e); }
}

function renderMessages(messages) {
    const container = document.getElementById('messagesContainer');
    if (!messages?.length) {
        container.innerHTML = '<div class="empty-state">💭 Нет сообщений<br>Отправьте первое!</div>';
        return;
    }

    const sorted = [...messages].sort((a, b) => new Date(a.messageCreateDate) - new Date(b.messageCreateDate));
    const safeToken = getSafeToken();

    container.innerHTML = sorted.map(msg => {
        const isOwn = msg.userId === state.user?.id;
        const edited = isEdited(msg);
        const sender = msg.user?.name || msg.messageCreator?.name || 'Unknown';
        const avatar = `${API}/User/avatar/${msg.userId}?access_token=${safeToken}`;
        const isDeleted = msg.isDeleted === true;

        if (msg.isSystemMessage) {
            return `<div class="message system"><div class="bubble">📢 ${escapeHtml(msg.messageText)}</div><div class="time">${formatDate(msg.messageCreateDate)}</div></div>`;
        }

        if (msg.fileName) {
            return `
                <div class="message ${isOwn ? 'own' : ''}${isDeleted ? ' deleted' : ''}">
                    <div class="sender">
                        <img src="${avatar}" onerror="this.src='${DEFAULT_AVATAR}'">
                        <span data-uid="${msg.userId}">${escapeHtml(sender)}</span>
                    </div>
                    <div class="bubble" style="padding:14px 18px;">
                        <div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap;">
                            <span style="font-size:28px;">${getFileIcon(msg.fileName)}</span>
                            <div style="flex:1;">
                                <div style="font-weight:600;">📁 ${escapeHtml(msg.fileName)}</div>
                                <div style="font-size:11px;color:var(--text-muted);">${formatFileSize(msg.fileSize)}</div>
                            </div>
                            ${!isDeleted ? `<button class="download-btn" data-id="${msg.messageId}" data-name="${escapeHtml(msg.fileName)}" style="background:#22c55e;border:none;border-radius:8px;padding:6px 14px;color:#fff;cursor:pointer;font-weight:600;">⬇️</button>` : '<span style="color:var(--text-muted);font-size:12px;">🗑️ Удалено</span>'}
                        </div>
                        ${msg.messageText && !msg.messageText.startsWith('📎') ? `<div style="margin-top:8px;font-size:13px;opacity:0.8;">${escapeHtml(msg.messageText)}${edited ? ' ✏️' : ''}</div>` : ''}
                    </div>
                    <div class="time">${formatDate(msg.messageCreateDate)}</div>
                    ${!isDeleted ? `<div class="actions">${isOwn ? `<button class="del-btn" data-id="${msg.messageId}">🗑️</button>` : ''}</div>` : ''}
                </div>
            `;
        }

        const text = escapeHtml(msg.messageText || '');
        return `
            <div class="message ${isOwn ? 'own' : ''}${isDeleted ? ' deleted' : ''}">
                <div class="sender">
                    <img src="${avatar}" onerror="this.src='${DEFAULT_AVATAR}'">
                    <span data-uid="${msg.userId}">${escapeHtml(sender)}</span>
                </div>
                <div class="bubble">${text}${edited ? ' <span style="font-size:10px;opacity:0.6;">✏️</span>' : ''}</div>
                <div class="time">${formatDate(msg.messageCreateDate)}</div>
                ${!isDeleted ? `
                    <div class="actions">
                        <button class="copy-btn" data-text="${text.replace(/'/g, "\\'")}">📋</button>
                        ${isOwn ? `<button class="edit-btn" data-id="${msg.messageId}" data-text="${text.replace(/'/g, "\\'")}" data-enc="${!!msg.encryptedData}">✏️</button>` : ''}
                        ${isOwn ? `<button class="del-btn" data-id="${msg.messageId}">🗑️</button>` : ''}
                    </div>
                ` : ''}
            </div>
        `;
    }).join('');

    // Event listeners
    container.querySelectorAll('.download-btn').forEach(btn => {
        btn.addEventListener('click', () => downloadFile(btn.dataset.id, btn.dataset.name));
    });
    container.querySelectorAll('.del-btn').forEach(btn => {
        btn.addEventListener('click', () => deleteMessage(btn.dataset.id));
    });
    container.querySelectorAll('.copy-btn').forEach(btn => {
        btn.addEventListener('click', () => copyToClipboard(btn.dataset.text));
    });
    container.querySelectorAll('.edit-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            openEditMessage(btn.dataset.id, btn.dataset.text, btn.dataset.enc === 'true');
        });
    });
    container.querySelectorAll('.sender span').forEach(el => {
        el.addEventListener('click', () => {
            const uid = el.dataset.uid;
            if (uid && uid !== state.user?.id) {
                showUserProfile(uid, el.textContent);
            }
        });
    });

    container.scrollTop = container.scrollHeight;
}

// ===== ОТПРАВКА СООБЩЕНИЯ =====
async function sendMessage() {
    const input = document.getElementById('messageInput');
    const text = input.value.trim();
    if (!text || !state.chat) return;

    if (!state.serverPublicKey) await loadServerPublicKey();

    let sessionKey = state.sessionKeys.get(state.chat.id);
    if (!sessionKey) sessionKey = await getSessionKeyForChat(state.chat.id);
    if (!sessionKey) {
        toast('🔒 Нет ключа шифрования', true);
        return;
    }

    try {
        const { encryptedData, iv } = await encryptForUsers(text, sessionKey);

        let serverEncrypted = '', serverIv = '';
        if (state.serverPublicKey) {
            try {
                const se = await encryptForServer(text, state.serverPublicKey);
                serverEncrypted = se.encryptedData;
                serverIv = se.iv;
            } catch (e) { console.error('Server encrypt failed:', e); }
        }

        const res = await fetch(`${API}/Message/dual-encrypted`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${state.token}` },
            body: JSON.stringify({
                encryptedForUsers: encryptedData,
                ivForUsers: iv,
                encryptedForServer: serverEncrypted,
                ivForServer: serverIv,
                userId: state.user.id,
                chatId: state.chat.id
            })
        });

        if (!res.ok) throw new Error(await res.text());

        input.value = '';
        await loadMessages(state.chat.id);
        await loadChats();
        state.unread.delete(state.chat.id);
        updateAllUnread();

        if (state.connection && state.connection.state === signalR.HubConnectionState.Connected) {
            await state.connection.invoke('SendEncryptedMessage', state.chat.id, state.user.id, state.user.name, encryptedData, iv);
        }
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

// ===== РЕДАКТИРОВАНИЕ СООБЩЕНИЯ =====
function openEditMessage(messageId, currentText, isEncrypted) {
    state.editingMessage = messageId;
    state.editingIsEncrypted = isEncrypted;
    document.getElementById('editMessageText').value = currentText;
    showModal('editMessageModal');
}

async function saveEditedMessage() {
    const text = document.getElementById('editMessageText').value.trim();
    if (!text || !state.editingMessage) return closeModals();

    try {
        if (state.editingIsEncrypted) {
            let key = state.sessionKeys.get(state.chat.id);
            if (!key) key = await getSessionKeyForChat(state.chat.id);
            if (!key) { toast('🔒 Нет ключа', true); return; }

            const { encryptedData, iv } = await encryptForUsers(text, key);
            await apiCall(`/Message/${state.editingMessage}`, {
                method: 'PUT',
                body: JSON.stringify({ messageId: state.editingMessage, encryptedData, iv })
            });
        } else {
            await apiCall(`/Message/${state.editingMessage}`, {
                method: 'PUT',
                body: JSON.stringify({ messageId: state.editingMessage, messageText: text })
            });
        }

        closeModals();
        state.editingMessage = null;
        await loadMessages(state.chat.id);
        await loadChats();
        toast('✅ Сообщение обновлено');
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

// ===== УДАЛЕНИЕ =====
async function deleteMessage(messageId) {
    if (!confirm('Удалить сообщение?')) return;
    try {
        const res = await apiCall('/Message/' + messageId, { method: 'DELETE' });
        if (res.ok) {
            await loadMessages(state.chat.id);
            await loadChats();
            toast('✅ Сообщение в корзине');
        }
    } catch (e) {
        toast('Ошибка удаления', true);
    }
}

// ===== ФАЙЛЫ =====
async function uploadFile() {
    const input = document.getElementById('fileInput');
    const file = input.files[0];
    if (!file || !state.chat) return;
    if (file.size > 50 * 1024 * 1024) return toast('Максимум 50MB', true);

    const fd = new FormData();
    fd.append('file', file);
    fd.append('chatId', state.chat.id);
    toast('⏳ Загрузка...');
    try {
        const res = await fetch(`${API}/File/upload`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${state.token}` },
            body: fd
        });
        if (res.ok) {
            input.value = '';
            toast('✅ Файл загружен');
            await loadMessages(state.chat.id);
            await loadChats();
            if (state.connection && state.connection.state === signalR.HubConnectionState.Connected) {
                await state.connection.invoke('SendMessage', state.chat.id, state.user.id, state.user.name, `📎 ${file.name}`);
            }
        }
    } catch (e) {
        toast('Ошибка загрузки', true);
    }
}

async function downloadFile(messageId, fileName) {
    try {
        const res = await fetch(`${API}/File/download/${messageId}`, {
            headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (res.ok) {
            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName || 'file';
            document.body.appendChild(a);
            a.click();
            setTimeout(() => { URL.revokeObjectURL(url); a.remove(); }, 100);
            toast('✅ Скачано');
        }
    } catch (e) {
        toast('Ошибка скачивания', true);
    }
}

// ===== КОРЗИНА =====
async function showTrashBin() {
    if (!state.chat) return toast('Выберите чат', true);
    const name = state.chatInfo?.chatName || state.chat.name || 'Чат';
    document.getElementById('trashChatName').innerText = name;
    showModal('trashModal');
    await loadDeletedMessages();
}

async function loadDeletedMessages() {
    if (!state.chat) return;
    const container = document.getElementById('deletedMessagesList');
    container.innerHTML = '<div class="empty-state">⏳ Загрузка...</div>';

    try {
        const res = await apiCall(`/Message/deleted/${state.chat.id}`);
        const messages = await res.json();

        if (!messages?.length) {
            container.innerHTML = '<div class="empty-state">📭 Нет удалённых сообщений</div>';
            return;
        }

        let key = state.sessionKeys.get(state.chat.id);
        if (!key) key = await getSessionKeyForChat(state.chat.id);

        const decrypted = [];
        for (const msg of messages) {
            let text = msg.messageText || (msg.fileName ? `📎 ${msg.fileName}` : '');
            if (msg.encryptedData && msg.iv && key) {
                try { text = await decryptMessage(msg.encryptedData, msg.iv, key); }
                catch (e) { text = '🔒 [Не удалось расшифровать]'; }
            } else if (msg.encryptedData && !key) {
                text = '🔒 [Зашифровано]';
            }
            decrypted.push({ ...msg, displayText: text });
        }

        container.innerHTML = decrypted.map(msg => `
            <div class="deleted-message-item">
                <div><strong>${escapeHtml(msg.messageCreator?.name || msg.user?.name || 'Unknown')}</strong>: ${escapeHtml(msg.displayText)}</div>
                <div class="meta">
                    <span>${formatDate(msg.messageCreateDate)}</span>
                    <button class="restore" data-id="${msg.messageId}">↩️ Восстановить</button>
                    <button class="permanent" data-id="${msg.messageId}">💀 Удалить навсегда</button>
                </div>
            </div>
        `).join('');

        container.querySelectorAll('.restore').forEach(btn => {
            btn.addEventListener('click', () => restoreMessage(btn.dataset.id));
        });
        container.querySelectorAll('.permanent').forEach(btn => {
            btn.addEventListener('click', () => permanentDeleteMessage(btn.dataset.id));
        });

    } catch (e) {
        container.innerHTML = '<div class="empty-state">❌ Ошибка загрузки</div>';
    }
}

async function restoreMessage(messageId) {
    try {
        const res = await apiCall(`/Message/restore/${messageId}`, { method: 'PATCH' });
        if (res.ok) {
            toast('✅ Сообщение восстановлено');
            await loadMessages(state.chat.id);
            await loadChats();
            await loadDeletedMessages();
        }
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

async function permanentDeleteMessage(messageId) {
    if (!confirm('⚠️ Удалить навсегда?')) return;
    try {
        const res = await apiCall(`/Message/permanent/${messageId}`, { method: 'DELETE' });
        if (res.ok) {
            toast('💀 Удалено навсегда');
            await loadDeletedMessages();
            await loadChats();
        }
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

// ===== ПОЛЬЗОВАТЕЛИ =====
async function showUserProfile(userId, userName) {
    try {
        const res = await apiCall(`/User/${userId}`);
        const user = await res.json();
        const avatar = `${API}/User/avatar/${userId}?access_token=${getSafeToken()}&t=${Date.now()}`;
        document.getElementById('userProfileContent').innerHTML = `
            <div style="text-align:center;">
                <img src="${avatar}" style="width:120px;height:120px;border-radius:50%;object-fit:cover;border:3px solid var(--accent);margin-bottom:16px;">
                <h3>${escapeHtml(user.name)}</h3>
                <p style="color:var(--text-muted);">📅 ${new Date(user.registerDate).toLocaleDateString()}</p>
                ${user.publicKey ? '<p style="color:#22c55e;">🔐 E2EE включён</p>' : '<p style="color:var(--text-muted);">⚠️ Нет ключа</p>'}
            </div>
        `;
        showModal('userProfileModal');
    } catch (e) {
        toast('Ошибка загрузки профиля', true);
    }
}

async function searchUsers() {
    const q = document.getElementById('searchUserInput').value.trim();
    const div = document.getElementById('searchResults');
    if (q.length < 2) { div.innerHTML = ''; return; }
    try {
        const res = await apiCall('/User/all');
        const users = await res.json();
        const filtered = users.filter(u => u.name.toLowerCase().includes(q.toLowerCase()) && u.id !== state.user?.id);
        if (!filtered.length) {
            div.innerHTML = '<div class="empty-state">👤 Никого не найдено</div>';
            return;
        }
        div.innerHTML = filtered.map(u => `
            <div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}">
                <img src="${API}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'">
                <div><strong>${escapeHtml(u.name)}</strong></div>
            </div>
        `).join('');
        div.querySelectorAll('.user-search-result').forEach(el => {
            el.addEventListener('click', () => createPrivateChat(el.dataset.id, el.dataset.name));
        });
    } catch (e) {
        div.innerHTML = '<div class="empty-state">❌ Ошибка</div>';
    }
}

// ===== ГРУППЫ =====
function showCreateGroup() {
    state.selectedMembers = [];
    updateSelected();
    document.getElementById('groupName').value = '';
    document.getElementById('groupMaxUsers').value = '100';
    document.getElementById('groupSearchInput').value = '';
    document.getElementById('groupSearchResults').innerHTML = '';
    showModal('createGroupModal');
}

async function searchGroupUsers() {
    const q = document.getElementById('groupSearchInput').value.trim();
    const div = document.getElementById('groupSearchResults');
    if (q.length < 2) { div.innerHTML = ''; return; }
    try {
        const res = await apiCall('/User/all');
        const users = await res.json();
        const filtered = users.filter(u =>
            u.name.toLowerCase().includes(q.toLowerCase()) &&
            u.id !== state.user?.id &&
            !state.selectedMembers.some(m => m.id === u.id)
        );
        div.innerHTML = filtered.map(u => `
            <div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}">
                <img src="${API}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'">
                <div>${escapeHtml(u.name)}</div>
            </div>
        `).join('');
        div.querySelectorAll('.user-search-result').forEach(el => {
            el.addEventListener('click', () => {
                state.selectedMembers.push({ id: el.dataset.id, name: el.dataset.name });
                updateSelected();
                document.getElementById('groupSearchInput').value = '';
                div.innerHTML = '';
            });
        });
    } catch (e) {
        div.innerHTML = '<div class="empty-state">❌ Ошибка</div>';
    }
}

function updateSelected() {
    const div = document.getElementById('selectedMembers');
    if (!state.selectedMembers.length) {
        div.innerHTML = '<div class="empty-state">👥 Нет участников</div>';
        return;
    }
    div.innerHTML = state.selectedMembers.map(m => `
        <div class="member-tag">${escapeHtml(m.name)}<button data-id="${m.id}">×</button></div>
    `).join('');
    div.querySelectorAll('.member-tag button').forEach(btn => {
        btn.addEventListener('click', () => {
            state.selectedMembers = state.selectedMembers.filter(m => m.id !== btn.dataset.id);
            updateSelected();
        });
    });
}

async function createGroup() {
    const name = document.getElementById('groupName').value.trim();
    const max = parseInt(document.getElementById('groupMaxUsers').value);
    if (!name) return toast('Введите название группы', true);
    if (!state.selectedMembers.length) return toast('Выберите участников', true);

    const ids = [state.user.id, ...state.selectedMembers.map(m => m.id)];
    try {
        const res = await apiCall('/Chat', {
            method: 'POST',
            body: JSON.stringify({ memberIds: ids, maxUsers: max, chatName: name })
        });
        if (res.ok) {
            const chat = await res.json();
            closeModals();
            await loadChats();
            await selectChat(chat.id, chat.chatName);
            toast(`✨ Группа "${name}" создана`);
        }
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

// ===== РЕДАКТИРОВАНИЕ ЧАТА =====
async function showEditChatModal() {
    if (!state.chatInfo) return;
    const isGroup = state.chatInfo.maxUsers > 2 || (state.chatInfo.users?.length > 2);
    document.getElementById('editChatTitle').innerText = isGroup ? '✏️ Редактировать группу' : '✏️ Редактировать чат';
    document.getElementById('editChatName').value = state.chatInfo.chatName;
    document.getElementById('leaveDeleteBtn').innerText = isGroup ? '🚪 Покинуть группу' : '🗑️ Удалить чат';

    if (isGroup) {
        document.getElementById('groupAvatarSection').style.display = 'block';
        document.getElementById('groupMembersSection').style.display = 'block';
        const url = await getGroupAvatarUrl(state.chat.id);
        document.getElementById('groupAvatarPreview').src = url;
        await renderMembers();
    } else {
        document.getElementById('groupAvatarSection').style.display = 'none';
        document.getElementById('groupMembersSection').style.display = 'none';
    }
    showModal('editChatModal');
}

async function renderMembers() {
    const members = state.chatInfo.users || [];
    const container = document.getElementById('membersList');
    container.innerHTML = members.map(m => `
        <div class="member-item">
            <div class="info">
                <img src="${API}/User/avatar/${m.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'">
                <strong>${escapeHtml(m.name)}</strong>${m.id === state.chatInfo.createdById ? ' 👑' : ''}
            </div>
            ${m.id !== state.user.id && state.chatInfo.createdById === state.user.id ? `<button class="remove" data-id="${m.id}" data-name="${escapeHtml(m.name)}">Удалить</button>` : ''}
        </div>
    `).join('');
    container.querySelectorAll('.remove').forEach(btn => {
        btn.addEventListener('click', () => removeMember(btn.dataset.id, btn.dataset.name));
    });
}

async function searchAddMember() {
    const q = document.getElementById('addMemberSearch').value.trim();
    const div = document.getElementById('addMemberResults');
    if (q.length < 2) { div.innerHTML = ''; return; }
    try {
        const res = await apiCall('/User/all');
        const users = await res.json();
        const existing = state.chatInfo.users.map(u => u.id);
        const filtered = users.filter(u =>
            u.name.toLowerCase().includes(q.toLowerCase()) &&
            u.id !== state.user.id &&
            !existing.includes(u.id)
        );
        div.innerHTML = filtered.map(u => `
            <div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}">
                <img src="${API}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'">
                <div>${escapeHtml(u.name)}</div>
            </div>
        `).join('');
        div.querySelectorAll('.user-search-result').forEach(el => {
            el.addEventListener('click', () => addMemberToGroup(el.dataset.id, el.dataset.name));
        });
    } catch (e) {
        div.innerHTML = '<div class="empty-state">❌ Ошибка</div>';
    }
}

async function addMemberToGroup(uid, uname) {
    try {
        await apiCall('/Chat/add-user', {
            method: 'POST',
            body: JSON.stringify({ chatId: state.chat.id, userId: uid })
        });
        state.chatInfo = await getChatInfo(state.chat.id);
        await renderMembers();
        document.getElementById('addMemberSearch').value = '';
        document.getElementById('addMemberResults').innerHTML = '';
        toast(`✅ ${uname} добавлен`);
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

async function removeMember(uid, uname) {
    if (!confirm(`Удалить ${uname}?`)) return;
    try {
        await apiCall('/Chat/remove-user', {
            method: 'POST',
            body: JSON.stringify({ chatId: state.chat.id, userId: uid })
        });
        state.chatInfo = await getChatInfo(state.chat.id);
        if (state.chatInfo) {
            await renderMembers();
            toast(`✅ ${uname} удалён`);
        } else {
            closeModals();
            state.chat = null;
            state.chatInfo = null;
            document.getElementById('messageInput').disabled = true;
            document.getElementById('sendBtn').disabled = true;
            await loadChats();
        }
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

async function saveChatEdit() {
    const name = document.getElementById('editChatName').value.trim();
    if (name && name !== state.chatInfo.chatName) {
        try {
            await apiCall('/Chat/' + state.chat.id, {
                method: 'PUT',
                body: JSON.stringify({ chatName: name })
            });
            state.chatInfo.chatName = name;
            await loadChats();
            toast('✅ Переименовано');
        } catch (e) {
            toast('Ошибка: ' + e.message, true);
        }
    }
    closeModals();
}

function leaveOrDelete() {
    const isGroup = state.chatInfo.maxUsers > 2 || (state.chatInfo.users?.length > 2);
    if (isGroup) leaveGroup();
    else deleteChat();
}

async function leaveGroup() {
    if (!confirm('Покинуть группу?')) return;
    try {
        await apiCall('/Chat/remove-user', {
            method: 'POST',
            body: JSON.stringify({ chatId: state.chat.id, userId: state.user.id })
        });
        closeModals();
        state.chat = null;
        state.chatInfo = null;
        document.getElementById('messageInput').disabled = true;
        document.getElementById('sendBtn').disabled = true;
        await loadChats();
        toast('✅ Группа покинута');
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

async function deleteChat() {
    if (!confirm('Удалить чат?')) return;
    try {
        await apiCall('/Chat/' + state.chat.id, { method: 'DELETE' });
        closeModals();
        state.chat = null;
        state.chatInfo = null;
        document.getElementById('messageInput').disabled = true;
        document.getElementById('sendBtn').disabled = true;
        await loadChats();
        toast('✅ Чат удалён');
    } catch (e) {
        toast('Ошибка: ' + e.message, true);
    }
}

// ===== SIGNALR =====
async function initSignalR() {
    state.connection = new signalR.HubConnectionBuilder()
        .withUrl("/messengerHub", { accessTokenFactory: () => state.token })
        .withAutomaticReconnect()
        .build();

    state.connection.on("ReceiveEncryptedMessage", async (userId, userName, encryptedData, iv, chatId) => {
        if (state.chat && state.chat.id === chatId) {
            await loadMessages(state.chat.id);
            await markChatAsRead(chatId);
        } else {
            const count = (state.unread.get(chatId) || 0) + 1;
            state.unread.set(chatId, count);
            await loadChats();
            toast(`🔒 Новое сообщение от ${userName}`);
        }
    });

    state.connection.on("ReceiveMessage", async (userId, userName, messageText, chatId) => {
        if (state.chat && state.chat.id === chatId) {
            await loadMessages(state.chat.id);
            await markChatAsRead(chatId);
        } else {
            const count = (state.unread.get(chatId) || 0) + 1;
            state.unread.set(chatId, count);
            await loadChats();
            toast(`💬 ${userName}: ${messageText?.slice(0, 40)}`);
        }
    });

    state.connection.on("UserOnline", (userId, isOnline) => {
        state.onlineUsers.set(userId, isOnline);
        loadChats();
    });

    state.connection.on("UserTyping", (userId, name) => {
        if (state.chat && userId !== state.user?.id) {
            const div = document.getElementById('typingIndicator');
            div.innerText = `${name} печатает...`;
            div.style.display = 'block';
            clearTimeout(window.typingTimeout);
            window.typingTimeout = setTimeout(() => div.style.display = 'none', 2000);
        }
    });

    state.connection.on("UserStoppedTyping", () => {
        document.getElementById('typingIndicator').style.display = 'none';
    });

    state.connection.on("NewChatCreated", async () => { await loadChats(); });

    try {
        await state.connection.start();
        console.log('✅ SignalR подключен');
        const res = await apiCall(`/Chat/user-chats/${state.user.id}`);
        const chats = await res.json();
        for (const chat of chats) {
            await state.connection.invoke('JoinChat', chat.id, state.user.id, state.user.name);
        }
        if (state.chat) {
            await state.connection.invoke('JoinChat', state.chat.id, state.user.id, state.user.name);
        }
    } catch (e) {
        console.error('SignalR error:', e);
    }
}

function startTyping() {
    if (state.connection?.state === signalR.HubConnectionState.Connected && state.chat) {
        state.connection.invoke('UserIsTyping', state.chat.id, state.user.id, state.user.name);
        clearTimeout(window.typingTimeout);
        window.typingTimeout = setTimeout(() => {
            if (state.connection?.state === signalR.HubConnectionState.Connected) {
                state.connection.invoke('UserStoppedTyping', state.chat.id, state.user.id);
            }
        }, 1000);
    }
}

// ===== ЭМОДЗИ ПИКЕР =====
function initEmojiPicker() {
    const container = document.querySelector('.emoji-picker-container');
    if (!container) return;

    // Загружаем emoji-picker динамически
    const script = document.createElement('script');
    script.src = 'https://cdn.jsdelivr.net/npm/emoji-picker-element@1.17.0/index.js';
    script.onload = () => {
        const picker = document.createElement('emoji-picker');
        picker.style.display = 'none';
        picker.style.position = 'absolute';
        picker.style.bottom = '60px';
        picker.style.left = '0';
        picker.style.zIndex = '1000';
        picker.style.background = 'var(--bg-card)';
        picker.style.borderRadius = 'var(--radius)';
        picker.style.boxShadow = 'var(--shadow)';
        picker.style.border = '1px solid var(--border)';
        picker.addEventListener('emoji-click', (e) => {
            const input = document.getElementById('messageInput');
            input.value += e.detail.unicode;
            picker.style.display = 'none';
            input.focus();
        });
        container.appendChild(picker);
        container.style.position = 'relative';

        const emojiBtn = document.getElementById('emojiBtn');
        if (emojiBtn) {
            emojiBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                picker.style.display = picker.style.display === 'none' ? 'block' : 'none';
            });
        }
        document.addEventListener('click', () => {
            if (picker.style.display === 'block') picker.style.display = 'none';
        });
    };
    document.head.appendChild(script);
}

// ===== ИНИЦИАЛИЗАЦИЯ =====
async function initApp() {
    if (!state.token) {
        const t = localStorage.getItem('token');
        const uid = localStorage.getItem('userId');
        if (t && uid) {
            state.token = t;
            state.user = { id: uid };
            const priv = await loadPrivateKey();
            if (priv) state.privateKey = await importPrivateKey(priv);
        } else return;
    }

    document.getElementById('authContainer').style.display = 'none';
    document.getElementById('appContainer').classList.add('active');

    try {
        const res = await apiCall('/User/profile');
        const profile = await res.json();
        state.user.name = profile.name;
        state.user.publicKey = profile.publicKey;
        document.getElementById('currentUserName').innerText = profile.name;
        const avatarUrl = `${API}/User/avatar/${state.user.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
        document.getElementById('sidebarAvatar').src = avatarUrl;
    } catch (e) { console.error(e); }

    await loadAllSessionKeys();
    await loadUnreadCounts();
    await loadChats();
    await loadServerPublicKey();
    await initSignalR();

    // Инициализируем emoji-picker
    initEmojiPicker();
}

function logout() {
    localStorage.clear();
    state.token = null;
    state.user = null;
    state.chat = null;
    state.privateKey = null;
    state.sessionKeys.clear();
    state.groupAvatarCache.clear();
    if (state.connection) state.connection.stop();
    document.getElementById('authContainer').style.display = 'flex';
    document.getElementById('appContainer').classList.remove('active');
}

// ===== EVENT BINDINGS =====
document.addEventListener('DOMContentLoaded', () => {
    console.log('🐺 Red Wolf Messenger загружается...');

    // Auth
    document.getElementById('doLoginBtn').addEventListener('click', handleLogin);
    document.getElementById('sendCodeBtn').addEventListener('click', requestCode);
    document.getElementById('verifyBtn').addEventListener('click', verifyRegistration);
    document.getElementById('backToRegisterBtn').addEventListener('click', () => {
        document.getElementById('verifyForm').style.display = 'none';
        document.getElementById('registerForm').style.display = 'flex';
    });
    document.querySelectorAll('.auth-tab').forEach(t => {
        t.addEventListener('click', () => switchTab(t.dataset.tab));
    });
    document.getElementById('loginPassword').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') handleLogin();
    });

    // App
    document.getElementById('logoutBtn').addEventListener('click', logout);
    document.getElementById('menuBtn').addEventListener('click', () => {
        document.getElementById('sidebar').classList.toggle('open');
    });
    document.getElementById('newChatBtn').addEventListener('click', () => {
        document.getElementById('searchUserInput').value = '';
        document.getElementById('searchResults').innerHTML = '';
        showModal('newChatModal');
    });
    document.getElementById('createGroupBtn').addEventListener('click', showCreateGroup);
    document.getElementById('trashBtn').addEventListener('click', showTrashBin);
    document.getElementById('refreshTrashBtn').addEventListener('click', loadDeletedMessages);

    // Messages
    document.getElementById('sendBtn').addEventListener('click', sendMessage);
    document.getElementById('messageInput').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });
    document.getElementById('messageInput').addEventListener('input', startTyping);
    document.getElementById('attachBtn').addEventListener('click', () => document.getElementById('fileInput').click());
    document.getElementById('fileInput').addEventListener('change', uploadFile);

    // Search
    document.getElementById('searchUserInput').addEventListener('input', searchUsers);
    document.getElementById('groupSearchInput').addEventListener('input', searchGroupUsers);
    document.getElementById('createGroupSubmit').addEventListener('click', createGroup);
    document.getElementById('addMemberSearch').addEventListener('input', searchAddMember);

    // Edit chat
    document.getElementById('saveChatEdit').addEventListener('click', saveChatEdit);
    document.getElementById('leaveDeleteBtn').addEventListener('click', leaveOrDelete);
    document.getElementById('uploadGroupAvatarBtn').addEventListener('click', () => document.getElementById('groupAvatarFile').click());
    document.getElementById('groupAvatarFile').addEventListener('change', (e) => uploadGroupAvatar(e.target.files[0]));
    document.getElementById('deleteGroupAvatarBtn').addEventListener('click', deleteGroupAvatar);

    // Profile
    document.getElementById('sidebarAvatar').addEventListener('click', showProfileModal);
    document.getElementById('currentUserName').addEventListener('click', showProfileModal);
    document.getElementById('updateProfileBtn').addEventListener('click', updateProfile);
    document.getElementById('uploadAvatarBtn').addEventListener('click', () => document.getElementById('avatarFile').click());
    document.getElementById('avatarFile').addEventListener('change', (e) => uploadAvatar(e.target.files[0]));
    document.getElementById('removeAvatarBtn').addEventListener('click', deleteAvatar);

    // Edit message
    document.getElementById('saveEditMessage').addEventListener('click', saveEditedMessage);

    // Close modals
    document.querySelectorAll('.close-modal').forEach(btn => {
        btn.addEventListener('click', closeModals);
    });

    // Click outside modal to close
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) closeModals();
        });
    });

    // Init if token exists
    if (localStorage.getItem('token')) {
        initApp();
    }
});

console.log('🐺 Red Wolf Messenger готов!');