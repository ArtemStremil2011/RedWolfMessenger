// ============ STATE ============
let currentUser = null, currentChat = null, currentChatInfo = null, token = null, connection = null;
let typingTimeout = null, editingMessageId = null, editingMessageIsEncrypted = false;
let selectedGroupMembers = [], tempPhoneNumber = null;
let onlineUsers = new Map();
let unreadCounts = new Map();
let lastMessagesCache = new Map();
let privateKey = null;
let sessionKeys = new Map();
let groupAvatarCache = new Map();
let serverPublicKey = null;
const API_BASE = '/api';
const DEFAULT_AVATAR = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%236b7280'%3E%3Cpath d='M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z'/%3E%3C/svg%3E";

// ============ ГОЛОСОВЫЕ СООБЩЕНИЯ ============
let mediaRecorder = null;
let audioChunks = [];
let recordingTimer = null;
let recordingSeconds = 0;
let isRecording = false;
let audioStream = null;

// ============ UTILITIES ============
function showToast(msg, isErr = false) {
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.textContent = msg;
    toast.style.background = isErr ? '#ef4444' : '#22c55e';
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 3000);
}

function escapeHtml(str) { if (!str) return ''; return str.replace(/[&<>]/g, function (m) { if (m === '&') return '&amp;'; if (m === '<') return '&lt;'; if (m === '>') return '&gt;'; return m; }); }
function formatFileSize(bytes) { if (!bytes) return '0 B'; const k = 1024, sizes = ['B', 'KB', 'MB', 'GB']; const i = Math.floor(Math.log(bytes) / Math.log(k)); return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]; }
function getFileIcon(name) { const e = name?.split('.').pop()?.toLowerCase(); if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(e)) return '🖼️'; if (['mp4', 'mov', 'avi', 'mkv'].includes(e)) return '🎥'; if (['pdf'].includes(e)) return '📕'; if (['doc', 'docx'].includes(e)) return '📘'; if (['xls', 'xlsx'].includes(e)) return '📊'; if (['txt', 'json', 'xml'].includes(e)) return '📄'; return '📎'; }
function formatTime(seconds) {
    const mins = String(Math.floor(seconds / 60)).padStart(2, '0');
    const secs = String(seconds % 60).padStart(2, '0');
    return `${mins}:${secs}`;
}

function formatMessageDate(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const msgDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const diffDays = Math.floor((today - msgDate) / (1000 * 60 * 60 * 24));
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    const timeStr = `${hours}:${minutes}`;
    if (diffDays === 0) return timeStr;
    if (diffDays === 1) return `Yesterday at ${timeStr}`;
    if (diffDays < 7) {
        const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        return `${days[date.getDay()]} at ${timeStr}`;
    }
    return `${date.getDate()}.${date.getMonth() + 1}.${date.getFullYear()} at ${timeStr}`;
}

function isMessageEdited(msg) {
    if (!msg.messageLastUpdateDate || !msg.messageCreateDate) return false;
    const createDate = new Date(msg.messageCreateDate);
    const updateDate = new Date(msg.messageLastUpdateDate);
    const diffMs = Math.abs(updateDate - createDate);
    return diffMs > 1000;
}

function copyToClipboard(text) { navigator.clipboard.writeText(text); showToast('📋 Copied!'); }
function closeModals() { document.querySelectorAll('.modal').forEach(m => m.classList.remove('active')); }
function showModal(id) { closeModals(); document.getElementById(id).classList.add('active'); }

function getSafeToken() { if (!token) return ''; return encodeURIComponent(token); }

async function api(url, opts = {}) {
    const res = await fetch(API_BASE + url, {
        headers: { 'Content-Type': 'application/json', ...(token ? { 'Authorization': `Bearer ${token}` } : {}) },
        ...opts
    });
    if (!res.ok) throw new Error(await res.text() || `HTTP ${res.status}`);
    return res;
}

// ============ PROFILE VIEW ============
async function showUserProfile(userId, userName) {
    try {
        const res = await api(`/User/${userId}`);
        const user = await res.json();
        const avatarUrl = `${API_BASE}/User/avatar/${userId}?access_token=${getSafeToken()}&t=${Date.now()}`;
        document.getElementById('userProfileContent').innerHTML = `
            <div class="profile-view">
                <img src="${avatarUrl}" onerror="this.src='${DEFAULT_AVATAR}'">
                <h3>${escapeHtml(user.name)}</h3>
                <p>📅 Joined: ${new Date(user.registerDate).toLocaleDateString()}</p>
                ${user.publicKey ? '<p>🔐 E2EE Enabled</p>' : '<p>⚠️ No encryption key</p>'}
            </div>
        `;
        showModal('userProfileModal');
    } catch (e) {
        console.error(e);
        showToast('Failed to load profile', true);
    }
}

// ============ CRYPTO ============
async function generateRSAKeys() {
    const keyPair = await crypto.subtle.generateKey(
        { name: "RSA-OAEP", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" },
        true, ["encrypt", "decrypt"]
    );
    return keyPair;
}

async function exportPublicKey(publicKey) {
    const exported = await crypto.subtle.exportKey("spki", publicKey);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function importPublicKey(base64Key) {
    const cleanKey = base64Key?.trim()?.replace(/\s/g, '') || '';
    const binary = Uint8Array.from(atob(cleanKey), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("spki", binary, { name: "RSA-OAEP", hash: "SHA-256" }, false, ["encrypt"]);
}

async function importPrivateKey(base64Key) {
    const cleanKey = base64Key?.trim()?.replace(/\s/g, '') || '';
    const binary = Uint8Array.from(atob(cleanKey), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("pkcs8", binary, { name: "RSA-OAEP", hash: "SHA-256" }, false, ["decrypt"]);
}

async function exportPrivateKey(privateKey) {
    const exported = await crypto.subtle.exportKey("pkcs8", privateKey);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function encryptWithPublicKey(data, publicKey) {
    const encrypted = await crypto.subtle.encrypt({ name: "RSA-OAEP" }, publicKey, data);
    return btoa(String.fromCharCode(...new Uint8Array(encrypted)));
}

async function generateSessionKey() {
    return await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
}

async function exportSessionKey(sessionKey) {
    const exported = await crypto.subtle.exportKey("raw", sessionKey);
    return btoa(String.fromCharCode(...new Uint8Array(exported)));
}

async function importSessionKey(base64Key) {
    const binary = Uint8Array.from(atob(base64Key), c => c.charCodeAt(0));
    return await crypto.subtle.importKey("raw", binary, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);
}

async function encryptForUsers(message, sessionKey) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const encoded = new TextEncoder().encode(message);
    const encrypted = await crypto.subtle.encrypt({ name: "AES-GCM", iv: iv }, sessionKey, encoded);
    return {
        encryptedData: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
        iv: btoa(String.fromCharCode(...iv))
    };
}

async function encryptForServer(message, serverPublicKey) {
    const aesKey = await crypto.subtle.generateKey(
        { name: "AES-GCM", length: 256 },
        true,
        ["encrypt", "decrypt"]
    );

    const iv = crypto.getRandomValues(new Uint8Array(12));
    const encoded = new TextEncoder().encode(message);
    const encryptedMessage = await crypto.subtle.encrypt({ name: "AES-GCM", iv: iv }, aesKey, encoded);

    const aesKeyRaw = await crypto.subtle.exportKey("raw", aesKey);
    const encryptedAesKey = await crypto.subtle.encrypt({ name: "RSA-OAEP" }, serverPublicKey, aesKeyRaw);

    const combined = new Uint8Array(encryptedAesKey.byteLength + encryptedMessage.byteLength);
    combined.set(new Uint8Array(encryptedAesKey), 0);
    combined.set(new Uint8Array(encryptedMessage), encryptedAesKey.byteLength);

    return {
        encryptedData: btoa(String.fromCharCode(...combined)),
        iv: btoa(String.fromCharCode(...iv))
    };
}

async function decryptMessage(encryptedBase64, ivBase64, sessionKey) {
    const encrypted = Uint8Array.from(atob(encryptedBase64), c => c.charCodeAt(0));
    const iv = Uint8Array.from(atob(ivBase64), c => c.charCodeAt(0));
    const decrypted = await crypto.subtle.decrypt({ name: "AES-GCM", iv: iv }, sessionKey, encrypted);
    return new TextDecoder().decode(decrypted);
}

// ============ SERVER PUBLIC KEY ============
async function loadServerPublicKey() {
    try {
        console.log("🔄 Loading server public key...");
        const res = await fetch(`${API_BASE}/User/server-public-key`);
        if (res.ok) {
            const data = await res.json();
            if (data.publicKey) {
                const cleanKey = data.publicKey.trim().replace(/\s/g, '');
                serverPublicKey = await importPublicKey(cleanKey);
                console.log("✅ Server public key loaded successfully!");
                return true;
            }
        }
        return false;
    } catch (e) {
        console.error("❌ Failed to load server public key:", e);
        return false;
    }
}

// ============ INDEXEDDB ============
const DB_NAME = "RedWolfKeys";
const DB_VERSION = 2;

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains("keys")) {
                db.createObjectStore("keys", { keyPath: "id" });
            }
        };
    });
}

async function savePrivateKeyToDB(privateKeyBase64) {
    const db = await openDatabase();
    const tx = db.transaction("keys", "readwrite");
    tx.objectStore("keys").put({ id: "privateKey", value: privateKeyBase64 });
    return new Promise((resolve, reject) => { tx.oncomplete = () => resolve(); tx.onerror = () => reject(tx.error); });
}

async function loadPrivateKeyFromDB() {
    const db = await openDatabase();
    const tx = db.transaction("keys", "readonly");
    const request = tx.objectStore("keys").get("privateKey");
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result?.value || null);
        request.onerror = () => reject(request.error);
    });
}

async function saveSessionKeyToDB(chatId, sessionKeyBase64) {
    const db = await openDatabase();
    const tx = db.transaction("keys", "readwrite");
    tx.objectStore("keys").put({ id: `sessionKey_${chatId}`, value: sessionKeyBase64 });
    return new Promise((resolve, reject) => { tx.oncomplete = () => resolve(); tx.onerror = () => reject(tx.error); });
}

async function loadAllSessionKeysFromDB() {
    const db = await openDatabase();
    const tx = db.transaction("keys", "readonly");
    const allKeys = await new Promise((resolve, reject) => {
        const request = tx.objectStore("keys").getAll();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });

    for (const item of allKeys) {
        if (item.id && item.id.startsWith('sessionKey_')) {
            const chatId = item.id.replace('sessionKey_', '');
            try {
                const sessionKey = await importSessionKey(item.value);
                sessionKeys.set(chatId, sessionKey);
                console.log(`Restored session key for chat ${chatId}`);
            } catch (e) {
                console.error(`Failed to restore session key for ${chatId}:`, e);
            }
        }
    }
}

async function getSessionKeyFromDB(chatId) {
    const db = await openDatabase();
    const tx = db.transaction("keys", "readonly");
    const request = tx.objectStore("keys").get(`sessionKey_${chatId}`);
    return new Promise((resolve, reject) => {
        request.onsuccess = async () => {
            if (request.result?.value) {
                try {
                    const key = await importSessionKey(request.result.value);
                    resolve(key);
                } catch (e) { resolve(null); }
            } else resolve(null);
        };
        request.onerror = () => reject(request.error);
    });
}

// ============ GROUP AVATAR HELPERS ============
async function getGroupAvatarUrl(chatId) {
    if (!token || !chatId) return DEFAULT_AVATAR;

    if (groupAvatarCache.has(chatId)) {
        const cached = groupAvatarCache.get(chatId);
        if (cached.url && cached.expiry > Date.now()) {
            return cached.url;
        }
    }

    try {
        const response = await fetch(`${API_BASE}/Chat/${chatId}/avatar`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            groupAvatarCache.set(chatId, { url: url, expiry: Date.now() + 5 * 60 * 1000 });
            return url;
        }
        return DEFAULT_AVATAR;
    } catch (e) {
        console.error('Error loading group avatar:', e);
        return DEFAULT_AVATAR;
    }
}

// ============ TRASH BIN METHODS ============
async function showTrashBin() {
    if (!currentChat) {
        showToast("Please select a chat first", true);
        return;
    }

    const chatName = currentChatInfo?.chatName || currentChat.name || "Chat";
    document.getElementById('trashChatName').innerText = chatName;
    showModal('trashModal');
    await loadDeletedMessages();
}

async function loadDeletedMessages() {
    if (!currentChat) return;

    const container = document.getElementById('deletedMessagesList');
    container.innerHTML = '<div class="empty-state">⏳ Loading...</div>';

    try {
        const res = await api(`/Message/deleted/${currentChat.id}`);
        const messages = await res.json();

        if (!messages || messages.length === 0) {
            container.innerHTML = '<div class="empty-state">📭 No deleted messages in this chat</div>';
            return;
        }

        let sessionKey = sessionKeys.get(currentChat.id);
        if (!sessionKey) {
            sessionKey = await getSessionKeyForChat(currentChat.id);
        }

        const decryptedMessages = [];
        for (const msg of messages) {
            let displayText = msg.messageText || (msg.fileName ? `📎 ${msg.fileName}` : '');

            if (msg.encryptedData && msg.iv && sessionKey) {
                try {
                    const decryptedText = await decryptMessage(msg.encryptedData, msg.iv, sessionKey);
                    displayText = decryptedText;
                } catch (e) {
                    console.error('Failed to decrypt deleted message:', e);
                    displayText = '🔒 [Cannot decrypt]';
                }
            } else if (msg.encryptedData && !sessionKey) {
                displayText = '🔒 [Encrypted - need key]';
            }

            decryptedMessages.push({ ...msg, displayText });
        }

        container.innerHTML = decryptedMessages.map(msg => {
            const time = formatMessageDate(msg.messageCreateDate);
            const sender = msg.messageCreator?.name || msg.user?.name || 'Unknown';
            const content = msg.displayText || (msg.fileName ? `📎 ${msg.fileName}` : '');

            return `
                <div class="deleted-message-item" data-mid="${msg.messageId}">
                    <div class="deleted-message-text">
                        <strong>${escapeHtml(sender)}</strong>: ${escapeHtml(content)}
                    </div>
                    <div class="deleted-message-meta">
                        <span>🕐 ${time}</span>
                        <button class="restore-btn" data-id="${msg.messageId}">↩️ Restore</button>
                        <button class="permanent-delete-btn" data-id="${msg.messageId}">💀 Permanent Delete</button>
                    </div>
                </div>
            `;
        }).join('');

        document.querySelectorAll('.restore-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                restoreMessage(btn.dataset.id);
            });
        });

        document.querySelectorAll('.permanent-delete-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                permanentDeleteMessage(btn.dataset.id);
            });
        });

    } catch (e) {
        console.error(e);
        container.innerHTML = '<div class="empty-state">❌ Error loading deleted messages</div>';
    }
}

async function restoreMessage(messageId) {
    try {
        const res = await api(`/Message/restore/${messageId}`, { method: 'PATCH' });
        if (res.ok) {
            showToast('✅ Message restored!');
            await loadMessages(currentChat.id);
            await loadChats();
            await loadDeletedMessages();
        }
    } catch (e) {
        showToast('Failed to restore: ' + e.message, true);
    }
}

async function permanentDeleteMessage(messageId) {
    if (!confirm('⚠️ This will permanently delete the message. Are you sure?')) return;

    try {
        const res = await api(`/Message/permanent/${messageId}`, { method: 'DELETE' });
        if (res.ok) {
            showToast('💀 Message permanently deleted');
            await loadDeletedMessages();
            await loadChats();
        }
    } catch (e) {
        showToast('Failed to delete: ' + e.message, true);
    }
}

// ============ AUTH ============
async function handleLogin() {
    const login = document.getElementById('loginLogin').value;
    const pwd = document.getElementById('loginPassword').value;
    if (!login || !pwd) return showToast('Fill all fields', true);
    try {
        const res = await fetch(API_BASE + '/User/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ login, password: pwd })
        });
        if (!res.ok) throw new Error(await res.text());
        const data = await res.json();
        token = data.token;
        currentUser = { id: data.userId };
        localStorage.setItem('token', token);
        localStorage.setItem('userId', data.userId);

        const profileRes = await api('/User/profile');
        const profile = await profileRes.json();
        currentUser.publicKey = profile.publicKey;
        currentUser.name = profile.name;

        const privateKeyBase64 = await loadPrivateKeyFromDB();
        if (privateKeyBase64) {
            privateKey = await importPrivateKey(privateKeyBase64);
            console.log("Private key loaded from IndexedDB");
        }

        await loadAllSessionKeysFromDB();
        await initApp();
    } catch (e) {
        console.error('Login error:', e);
        showToast('Login failed: ' + e.message, true);
    }
}

async function requestCode() {
    const phone = document.getElementById('regPhone').value.trim();
    const name = document.getElementById('regName').value.trim();
    const pwd = document.getElementById('regPassword').value;

    if (!phone || !name || !pwd) return showToast('Fill all fields', true);
    if (pwd.length < 6) return showToast('Password min 6 chars', true);

    console.log("📱 Requesting code for:", phone);

    try {
        const res = await fetch(API_BASE + '/User/request-verification', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: phone, name, password: pwd })
        });

        if (!res.ok) {
            const error = await res.text();
            throw new Error(error);
        }

        const data = await res.json();
        console.log("📦 Response:", data);

        tempPhoneNumber = data.phoneNumber || phone;
        console.log("📱 tempPhoneNumber set to:", tempPhoneNumber);

        document.getElementById('registerForm').style.display = 'none';
        document.getElementById('verifyForm').style.display = 'flex';
        document.getElementById('verifyCode').value = data.code || '';

        if (data.code) {
            showToast('✅ Code: ' + data.code);
        } else {
            showToast('✅ Code sent to ' + phone);
        }
    } catch (e) {
        console.error('Request code error:', e);
        showToast('Failed to send code: ' + e.message, true);
    }
}

async function verifyReg() {
    const code = document.getElementById('verifyCode').value.trim();
    console.log("🔍 Verifying with phone:", tempPhoneNumber);
    console.log("🔍 Code:", code);

    if (!code || code.length !== 6) return showToast('Enter 6-digit code', true);
    if (!tempPhoneNumber) return showToast('❌ Request code first!', true);

    try {
        const res = await fetch(API_BASE + '/User/verify-and-register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                phoneNumber: tempPhoneNumber,
                code: code
            })
        });

        if (!res.ok) {
            const error = await res.text();
            throw new Error(error);
        }

        const data = await res.json();
        console.log("✅ Verification success:", data);

        token = data.token;
        currentUser = { id: data.userId };
        localStorage.setItem('token', token);
        localStorage.setItem('userId', data.userId);

        const keyPair = await generateRSAKeys();
        const publicKeyBase64 = await exportPublicKey(keyPair.publicKey);
        const privateKeyBase64 = await exportPrivateKey(keyPair.privateKey);

        await fetch(API_BASE + '/User/public-key', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify({ publicKey: publicKeyBase64 })
        });

        await savePrivateKeyToDB(privateKeyBase64);
        privateKey = keyPair.privateKey;
        currentUser.publicKey = publicKeyBase64;

        await initApp();
    } catch (e) {
        console.error('Verification error:', e);
        showToast('Verification failed: ' + e.message, true);
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

// ============ PROFILE ============
async function showProfileModal() {
    try {
        const res = await api('/User/profile');
        const user = await res.json();
        document.getElementById('profileName').value = user.name;
        document.getElementById('profileNewPassword').value = '';
        const avatarUrl = `${API_BASE}/User/avatar/${user.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
        document.getElementById('profileAvatar').src = avatarUrl;
        showModal('profileModal');
    } catch (e) { showToast('Error loading profile', true); }
}

async function updateProfile() {
    const newName = document.getElementById('profileName').value.trim();
    const newPassword = document.getElementById('profileNewPassword').value;
    const updateData = {};
    if (newName) updateData.name = newName;
    if (newPassword) updateData.newPassword = newPassword;
    if (Object.keys(updateData).length === 0) { closeModals(); return; }
    try {
        const res = await api('/User/update-profile', { method: 'PUT', body: JSON.stringify(updateData) });
        if (res.ok) {
            const updatedUser = await res.json();
            if (currentUser) currentUser.name = updatedUser.name;
            document.getElementById('currentUserName').innerText = updatedUser.name;
            showToast('✅ Profile updated!');
            closeModals();
            await loadChats();
        } else showToast('Update failed', true);
    } catch (e) { showToast('Error', true); }
}

async function uploadAvatar(file) {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) return showToast('Max 5MB', true);
    const fd = new FormData();
    fd.append('file', file);
    try {
        const res = await fetch(`${API_BASE}/User/upload-avatar`, { method: 'POST', headers: { 'Authorization': `Bearer ${token}` }, body: fd });
        if (res.ok) {
            const avatarUrl = `${API_BASE}/User/avatar/${currentUser.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
            document.getElementById('sidebarAvatar').src = avatarUrl;
            document.getElementById('profileAvatar').src = avatarUrl;
            await loadChats();
            showToast('✅ Avatar updated!');
        } else showToast('Upload failed', true);
    } catch (e) { showToast('Error', true); }
}

async function deleteAvatar() {
    if (!confirm('Remove avatar?')) return;
    try {
        const res = await fetch(`${API_BASE}/User/avatar`, { method: 'DELETE', headers: { 'Authorization': `Bearer ${token}` } });
        if (res.ok) {
            document.getElementById('sidebarAvatar').src = DEFAULT_AVATAR;
            document.getElementById('profileAvatar').src = DEFAULT_AVATAR;
            await loadChats();
            showToast('✅ Avatar removed');
        } else showToast('Delete failed', true);
    } catch (e) { showToast('Error', true); }
}

// ============ GROUP AVATAR ============
async function uploadGroupAvatar(file) {
    if (!file || !currentChat) return;
    if (file.size > 5 * 1024 * 1024) return showToast('Max 5MB', true);
    const fd = new FormData();
    fd.append('file', file);
    try {
        const res = await fetch(`${API_BASE}/Chat/${currentChat.id}/avatar`, { method: 'POST', headers: { 'Authorization': `Bearer ${token}` }, body: fd });
        if (res.ok) {
            const data = await res.json();
            showToast('✅ Group avatar updated!');
            if (currentChatInfo) currentChatInfo.avatarPath = data.avatarPath;
            groupAvatarCache.delete(currentChat.id);
            await loadChats();
            const newAvatarUrl = await getGroupAvatarUrl(currentChat.id);
            document.getElementById('groupAvatarPreview').src = newAvatarUrl;
        } else showToast('Upload failed', true);
    } catch (e) { showToast('Error', true); }
}

async function deleteGroupAvatar() {
    if (!confirm('Remove group avatar?')) return;
    try {
        const res = await fetch(`${API_BASE}/Chat/${currentChat.id}/avatar`, { method: 'DELETE', headers: { 'Authorization': `Bearer ${token}` } });
        if (res.ok) {
            showToast('✅ Group avatar removed');
            if (currentChatInfo) currentChatInfo.avatarPath = null;
            groupAvatarCache.delete(currentChat.id);
            document.getElementById('groupAvatarPreview').src = DEFAULT_AVATAR;
            await loadChats();
        } else showToast('Delete failed', true);
    } catch (e) { showToast('Error', true); }
}

// ============ UNREAD ============
async function loadUnreadCounts() {
    try {
        const res = await api('/Message/unread');
        const counts = await res.json();
        unreadCounts.clear();
        for (const [chatId, count] of Object.entries(counts)) unreadCounts.set(chatId, count);
        updateAllUnreadBadges();
    } catch (e) { console.error(e); }
}

function updateAllUnreadBadges() {
    document.querySelectorAll('.chat-item').forEach(item => {
        const chatId = item.dataset.id;
        const count = unreadCounts.get(chatId) || 0;
        const nameDiv = item.querySelector('.chat-name');
        const existingBadge = nameDiv?.querySelector('.unread-badge');
        if (count > 0) {
            if (existingBadge) existingBadge.textContent = count > 99 ? '99+' : count;
            else if (nameDiv) {
                const badge = document.createElement('span');
                badge.className = 'unread-badge';
                badge.textContent = count > 99 ? '99+' : count;
                nameDiv.appendChild(badge);
            }
        } else if (existingBadge) existingBadge.remove();
    });
}

function updateUnreadBadge(chatId, count) {
    if (count > 0) unreadCounts.set(chatId, count);
    else unreadCounts.delete(chatId);
    const chatItem = document.querySelector(`.chat-item[data-id="${chatId}"]`);
    if (chatItem) {
        const nameDiv = chatItem.querySelector('.chat-name');
        const existingBadge = nameDiv?.querySelector('.unread-badge');
        if (count > 0) {
            if (existingBadge) existingBadge.textContent = count > 99 ? '99+' : count;
            else if (nameDiv) {
                const badge = document.createElement('span');
                badge.className = 'unread-badge';
                badge.textContent = count > 99 ? '99+' : count;
                nameDiv.appendChild(badge);
            }
        } else if (existingBadge) existingBadge.remove();
    }
}

async function markChatAsRead(chatId) {
    try {
        await api(`/Message/${chatId}/mark-read`, { method: 'POST' });
        unreadCounts.delete(chatId);
        updateUnreadBadge(chatId, 0);
    } catch (e) { console.error(e); }
}

// ============ CHAT UI ============
async function addChatToSidebar(chat) {
    const container = document.getElementById('chatsList');
    const isGroup = chat.maxUsers > 2 || (chat.users?.length > 2);
    const name = isGroup ? chat.chatName : (chat.otherUser?.name || chat.chatName);
    const online = (!isGroup && chat.otherUser?.id) ? onlineUsers.get(chat.otherUser.id) === true : false;
    const unreadCount = unreadCounts.get(chat.id) || 0;
    const lastMessage = lastMessagesCache.get(chat.id);

    let avatarUrl = DEFAULT_AVATAR;
    if (isGroup) {
        avatarUrl = await getGroupAvatarUrl(chat.id);
    } else if (chat.otherUser?.id) {
        avatarUrl = `${API_BASE}/User/avatar/${chat.otherUser.id}?access_token=${getSafeToken()}&t=${Date.now()}`;
    }

    let previewHtml = '';
    if (lastMessage) {
        const isOwn = lastMessage.userId === currentUser?.id;
        const senderName = isOwn ? 'You' : (lastMessage.user?.name || lastMessage.messageCreator?.name || 'Unknown');
        let previewText = lastMessage.messageText || (lastMessage.fileName ? '📎 File' : '');
        if (!previewText && lastMessage.encryptedData) previewText = '🔒 Encrypted';
        previewText = previewText?.length > 50 ? previewText.substring(0, 47) + '...' : previewText;
        previewHtml = `<div class="chat-preview"><strong>${escapeHtml(senderName)}:</strong> ${escapeHtml(previewText || '')}</div>`;
    }

    const html = `<div class="chat-item" data-id="${chat.id}" data-name="${escapeHtml(name)}">
        <div style="display:flex;align-items:center;gap:12px">
            <div class="chat-avatar-container">
                <img src="${avatarUrl}" style="width:48px;height:48px;border-radius:50%;object-fit:cover" onerror="this.src='${DEFAULT_AVATAR}'">
                ${!isGroup ? `<div class="online-dot ${online ? 'online' : 'offline'}"></div>` : ''}
            </div>
            <div class="chat-info">
                <div class="chat-name">${escapeHtml(name)}${isGroup ? '<span class="chat-badge">GROUP</span>' : ''}</div>
                ${previewHtml}
            </div>
        </div>
    </div>`;

    const existingItem = container.querySelector(`.chat-item[data-id="${chat.id}"]`);
    if (existingItem) existingItem.outerHTML = html;
    else container.insertAdjacentHTML('beforeend', html);
    updateUnreadBadge(chat.id, unreadCount);
    const newItem = container.querySelector(`.chat-item[data-id="${chat.id}"]`);
    if (newItem) newItem.addEventListener('click', () => selectChat(chat.id, name));
}

// ============ CORE ============
async function loadUser() {
    try {
        const res = await api('/User/profile');
        const user = await res.json();
        currentUser = { ...currentUser, ...user };
        document.getElementById('currentUserName').innerText = user.name;
        const avUrl = API_BASE + '/User/avatar/' + currentUser.id + '?access_token=' + getSafeToken() + '&t=' + Date.now();
        document.getElementById('sidebarAvatar').src = avUrl;
    } catch (e) { console.error(e); }
}

async function loadLastMessageForChat(chat) {
    try {
        const messagesRes = await api(`/Chat/${chat.id}/messages?page=1&pageSize=1`);
        const messagesData = await messagesRes.json();
        if (messagesData.messages && messagesData.messages.length > 0) {
            const lastMsg = messagesData.messages[0];
            if (lastMsg.encryptedData && lastMsg.iv) {
                const sessionKey = sessionKeys.get(chat.id) || await getSessionKeyFromDB(chat.id);
                if (sessionKey) {
                    try {
                        const decryptedText = await decryptMessage(lastMsg.encryptedData, lastMsg.iv, sessionKey);
                        lastMsg.messageText = decryptedText;
                    } catch (e) { /* keep encrypted */ }
                }
            }
            lastMessagesCache.set(chat.id, lastMsg);
        } else {
            lastMessagesCache.delete(chat.id);
        }
    } catch (e) { lastMessagesCache.delete(chat.id); }
}

async function loadChats() {
    try {
        if (!token) return;
        const [chatsRes, unreadRes] = await Promise.all([api(`/Chat/user-chats/${currentUser.id}`), api('/Message/unread')]);
        const chats = await chatsRes.json();
        const unreadData = await unreadRes.json();
        unreadCounts.clear();
        for (const [chatId, count] of Object.entries(unreadData)) unreadCounts.set(chatId, count);

        await Promise.all(chats.map(chat => loadLastMessageForChat(chat)));

        const container = document.getElementById('chatsList');
        if (!chats?.length) { container.innerHTML = '<div class="empty-state">💬 No chats yet<br>✨ Start a new conversation</div>'; return; }

        container.innerHTML = '';
        for (const chat of chats) {
            await addChatToSidebar(chat);
        }

    } catch (e) { console.error(e); }
}

async function getChatInfo(chatId) { try { const res = await api('/Chat/' + chatId); return await res.json(); } catch (e) { return null; } }

async function getSessionKeyForChat(chatId) {
    if (sessionKeys.has(chatId)) {
        return sessionKeys.get(chatId);
    }

    try {
        const dbKey = await getSessionKeyFromDB(chatId);
        if (dbKey) {
            sessionKeys.set(chatId, dbKey);
            return dbKey;
        }
    } catch (e) { console.error(`DB error:`, e); }

    if (!privateKey) {
        showToast("❌ Private key not loaded. Please re-login.", true);
        return null;
    }

    try {
        const res = await fetch(`${API_BASE}/Chat/${chatId}/session-key`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!res.ok) return null;

        const data = await res.json();
        if (!data.encryptedKey) return null;

        const encryptedBytes = Uint8Array.from(atob(data.encryptedKey), c => c.charCodeAt(0));
        const decryptedKeyBuffer = await crypto.subtle.decrypt({ name: "RSA-OAEP" }, privateKey, encryptedBytes);

        const sessionKey = await crypto.subtle.importKey("raw", decryptedKeyBuffer, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);

        sessionKeys.set(chatId, sessionKey);
        const exportedKey = btoa(String.fromCharCode(...new Uint8Array(decryptedKeyBuffer)));
        await saveSessionKeyToDB(chatId, exportedKey);

        return sessionKey;
    } catch (e) {
        console.error(`[getSessionKeyForChat] Failed:`, e);
        return null;
    }
}

async function createPrivateChat(uid, uname) {
    try {
        const pubKeyRes = await api(`/User/public-key/${uid}`);
        const pubKeyData = await pubKeyRes.json();
        if (!pubKeyData.publicKey) {
            showToast(`User ${uname} has no public key. Ask them to re-register.`, true);
            return;
        }
        const otherPublicKey = await importPublicKey(pubKeyData.publicKey);
        const sessionKey = await generateSessionKey();
        const sessionKeyRaw = await exportSessionKey(sessionKey);
        const myPublicKey = await importPublicKey(currentUser.publicKey);
        const encryptedForMe = await encryptWithPublicKey(Uint8Array.from(atob(sessionKeyRaw), c => c.charCodeAt(0)), myPublicKey);
        const encryptedForOther = await encryptWithPublicKey(Uint8Array.from(atob(sessionKeyRaw), c => c.charCodeAt(0)), otherPublicKey);
        const chatRes = await api('/Chat', { method: 'POST', body: JSON.stringify({ memberIds: [currentUser.id, uid], maxUsers: 2 }) });
        const chat = await chatRes.json();
        const encryptedKeys = { [currentUser.id]: encryptedForMe, [uid]: encryptedForOther };
        await api(`/Chat/${chat.id}/session-keys`, { method: 'POST', body: JSON.stringify({ encryptedKeys }) });

        sessionKeys.set(chat.id, sessionKey);
        await saveSessionKeyToDB(chat.id, sessionKeyRaw);

        closeModals();
        await loadChats();
        await selectChat(chat.id, chat.chatName);
        showToast(`✨ Chat with ${uname} created`);
    } catch (e) { showToast('Error creating chat: ' + e.message, true); }
}

async function selectChat(chatId, chatName) {
    currentChat = { id: chatId, name: chatName };
    currentChatInfo = await getChatInfo(chatId);
    const isGroup = currentChatInfo && (currentChatInfo.maxUsers > 2 || (currentChatInfo.users?.length > 2));
    const title = isGroup ? currentChatInfo.chatName : (currentChatInfo?.otherUser?.name || chatName);
    const otherUserId = !isGroup && currentChatInfo?.otherUser?.id;

    const titleHtml = otherUserId
        ? `<h2 style="cursor:pointer" id="chatTitleBtn">${escapeHtml(title)}</h2>`
        : `<h2>${escapeHtml(title)}</h2>`;

    document.getElementById('chatHeader').innerHTML = `<div class="chat-header-top">
        ${titleHtml}
        <div class="chat-actions">
            <button class="trash-chat-btn" id="trashChatBtn">🗑️ Trash</button>
            <button class="edit-chat-btn" id="editChatBtn">✏️ Edit</button>
        </div>
    </div>`;

    if (otherUserId) {
        document.getElementById('chatTitleBtn')?.addEventListener('click', () => showUserProfile(otherUserId, title));
    }

    document.getElementById('editChatBtn')?.addEventListener('click', showEditChatModal);
    document.getElementById('trashChatBtn')?.addEventListener('click', showTrashBin);
    document.getElementById('messageInput').disabled = false;
    document.getElementById('sendBtn').disabled = false;

    const key = await getSessionKeyForChat(chatId);
    if (!key) showToast("⚠️ Could not load encryption key for this chat", true);

    await markChatAsRead(chatId);
    await loadMessages(chatId);
    await loadChats();

    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        await connection.invoke('JoinChat', chatId, currentUser.id, currentUser.name);
    }
    if (window.innerWidth <= 768) document.getElementById('sidebar').classList.remove('open');
}

async function loadMessages(chatId) {
    try {
        const res = await api(`/Chat/${chatId}/messages?page=1&pageSize=100`);
        const data = await res.json();
        const sessionKey = sessionKeys.get(chatId);
        const decryptedMessages = [];
        for (const msg of (data.messages || [])) {
            if (msg.encryptedData && msg.iv && sessionKey) {
                try {
                    const decryptedText = await decryptMessage(msg.encryptedData, msg.iv, sessionKey);
                    decryptedMessages.push({ ...msg, messageText: decryptedText, isDecrypted: true });
                } catch (e) {
                    decryptedMessages.push({ ...msg, messageText: "🔒 [Encrypted]", isDecrypted: false });
                }
            } else {
                decryptedMessages.push(msg);
            }
        }
        renderMessages(decryptedMessages);
        if (data.messages && data.messages.length > 0) {
            const lastMsg = data.messages[0];
            if (lastMsg.encryptedData && lastMsg.iv && sessionKey) {
                try {
                    lastMsg.messageText = await decryptMessage(lastMsg.encryptedData, lastMsg.iv, sessionKey);
                } catch (e) { }
            }
            lastMessagesCache.set(chatId, lastMsg);
        } else {
            lastMessagesCache.delete(chatId);
        }
    } catch (e) { console.error(e); }
}

function renderMessages(messages) {
    const container = document.getElementById('messagesContainer');
    if (!messages?.length) { container.innerHTML = '<div class="empty-state">💭 No messages yet<br>Send the first message!</div>'; return; }
    const sorted = [...messages].sort((a, b) => new Date(a.messageCreateDate) - new Date(b.messageCreateDate));
    const safeToken = getSafeToken();
    container.innerHTML = sorted.map(msg => {
        const isOwn = msg.userId === currentUser.id;
        const edited = isMessageEdited(msg);
        const avatar = API_BASE + '/User/avatar/' + msg.userId + '?access_token=' + safeToken;
        const sender = msg.user?.name || msg.messageCreator?.name || 'Unknown';
        const isDeleted = msg.isDeleted === true;
        const deletedClass = isDeleted ? 'deleted-message' : '';
        const isEncrypted = !!msg.encryptedData;
        const isVoice = msg.messageType === 'voice' || msg.contentType?.startsWith('audio/');

        // Голосовое сообщение
        if (isVoice && msg.fileName) {
            const duration = msg.duration || 0;
            const durationStr = formatTime(duration);
            return `<div class="message ${isOwn ? 'own' : ''} ${deletedClass}" data-mid="${msg.messageId}">
                <div class="message-header">
                    <img src="${avatar}" onerror="this.src='${DEFAULT_AVATAR}'>
                    <span class="message-sender" data-userid="${msg.userId}" data-username="${escapeHtml(sender)}">${escapeHtml(sender)}</span>
                </div>
                <div class="voice-message-player" data-mid="${msg.messageId}" data-duration="${duration}">
                    <button class="play-btn" data-mid="${msg.messageId}">▶️</button>
                    <div class="progress-bar">
                        <div class="progress-fill" id="progress-fill-${msg.messageId}"></div>
                    </div>
                    <span class="voice-duration">${durationStr}</span>
                </div>
                ${msg.messageText && !msg.messageText.startsWith('🎤') ? `<div class="message-caption">${escapeHtml(msg.messageText)}</div>` : ''}
                <div class="message-time">🕐 ${formatMessageDate(msg.messageCreateDate)}</div>
                ${!isDeleted ? `<div class="message-actions">${isOwn ? `<button class="del-msg-btn" data-id="${msg.messageId}">🗑️ Delete</button>` : ''}</div>` : ''}
            </div>`;
        }

        // Файл
        if (msg.fileName) {
            return `<div class="message ${isOwn ? 'own' : ''} ${deletedClass}" data-mid="${msg.messageId}">
                <div class="message-header">
                    <img src="${avatar}" onerror="this.src='${DEFAULT_AVATAR}'>
                    <span class="message-sender" data-userid="${msg.userId}" data-username="${escapeHtml(sender)}">${escapeHtml(sender)}</span>
                </div>
                <div class="message-file">
                    <div class="file-icon">${getFileIcon(msg.fileName)}</div>
                    <div class="file-info">
                        <div class="file-name">📁 ${escapeHtml(msg.fileName)}</div>
                        <div class="file-size">${formatFileSize(msg.fileSize)}</div>
                        ${msg.duration ? `<div class="file-duration">⏱️ ${formatTime(msg.duration)}</div>` : ''}
                    </div>
                    ${!isDeleted ? `<button class="download-btn" data-id="${msg.messageId}" data-name="${escapeHtml(msg.fileName)}">⬇️ DOWNLOAD</button>` : '<span style="color:#6b7280; font-size:12px;">🗑️ Deleted</span>'}
                </div>
                ${msg.messageText && !msg.messageText.startsWith('📎') ? `<div class="message-caption">${escapeHtml(msg.messageText)}${edited ? ' ✏️' : ''}</div>` : ''}
                <div class="message-time">🕐 ${formatMessageDate(msg.messageCreateDate)}</div>
                ${!isDeleted ? `<div class="message-actions">${isOwn ? `<button class="del-msg-btn" data-id="${msg.messageId}">🗑️ Delete</button>` : ''}</div>` : ''}
            </div>`;
        }

        // Системное сообщение
        if (msg.isSystemMessage) return `<div class="message system ${deletedClass}"><div class="message-content">📢 ${escapeHtml(msg.messageText)}</div><div class="message-time">${formatMessageDate(msg.messageCreateDate)}</div></div>`;

        // Текстовое сообщение
        const textEsc = escapeHtml(msg.messageText).replace(/'/g, "\\'");
        const editBtn = `<button class="edit-msg-btn" data-id="${msg.messageId}" data-text="${textEsc}" data-encrypted="${isEncrypted}">✏️ Edit</button>`;
        return `<div class="message ${isOwn ? 'own' : ''} ${deletedClass}">
            <div class="message-header">
                <img src="${avatar}" onerror="this.src='${DEFAULT_AVATAR}'>
                <span class="message-sender" data-userid="${msg.userId}" data-username="${escapeHtml(sender)}">${escapeHtml(sender)}</span>
            </div>
            <div class="message-content">${escapeHtml(msg.messageText)}${edited ? ' <span style="font-size:10px; opacity:0.6;">✏️ edited</span>' : ''}</div>
            <div class="message-time">${formatMessageDate(msg.messageCreateDate)}</div>
            ${!isDeleted ? `<div class="message-actions">
                <button class="copy-btn" data-text="${textEsc}">📋 Copy</button>
                ${isOwn ? editBtn : ''}
                ${isOwn ? `<button class="del-msg-btn" data-id="${msg.messageId}">🗑️ Delete</button>` : ''}
            </div>` : ''}
        </div>`;
    }).join('');

    document.querySelectorAll('.download-btn').forEach(btn => { btn.addEventListener('click', (e) => { e.stopPropagation(); downloadFile(btn.dataset.id, btn.dataset.name); }); });
    document.querySelectorAll('.del-msg-btn').forEach(btn => btn.addEventListener('click', (e) => { e.stopPropagation(); deleteMessage(btn.dataset.id); }));
    document.querySelectorAll('.copy-btn').forEach(btn => btn.addEventListener('click', () => copyToClipboard(btn.dataset.text)));
    document.querySelectorAll('.edit-msg-btn').forEach(btn => btn.addEventListener('click', () => {
        editMessage(btn.dataset.id, btn.dataset.text, btn.dataset.encrypted === 'true');
    }));
    document.querySelectorAll('.message-sender').forEach(el => {
        el.addEventListener('click', (e) => {
            e.stopPropagation();
            const userId = el.dataset.userid;
            const userName = el.dataset.username;
            if (userId && userId !== currentUser.id) {
                showUserProfile(userId, userName);
            }
        });
    });

    // ===== ПЛЕЕР ГОЛОСОВЫХ =====
    document.querySelectorAll('.voice-message-player').forEach(player => {
        const playBtn = player.querySelector('.play-btn');
        const progressFill = player.querySelector('.progress-fill');
        const durationSpan = player.querySelector('.voice-duration');
        const messageId = player.dataset.mid;
        const totalDuration = parseInt(player.dataset.duration) || 0;

        let audio = null;
        let isPlaying = false;
        let progressInterval = null;
        let currentAudioUrl = null;

        durationSpan.textContent = formatTime(totalDuration);

        playBtn.addEventListener('click', async (e) => {
            e.stopPropagation();

            if (audio && isPlaying) {
                audio.pause();
                isPlaying = false;
                playBtn.textContent = '▶️';
                clearInterval(progressInterval);
                return;
            }

            if (audio && !isPlaying) {
                try {
                    await audio.play();
                    isPlaying = true;
                    playBtn.textContent = '⏸️';
                    progressInterval = setInterval(() => {
                        if (audio && audio.duration > 0) {
                            const progress = (audio.currentTime / audio.duration) * 100;
                            progressFill.style.width = `${Math.min(progress, 100)}%`;
                        }
                    }, 100);
                } catch (e) {
                    console.warn('Play error:', e);
                }
                return;
            }

            try {
                showToast('⏳ Loading voice...');
                const res = await fetch(`${API_BASE}/File/download/${messageId}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!res.ok) throw new Error('Download failed');

                const blob = await res.blob();
                currentAudioUrl = URL.createObjectURL(blob);
                audio = new Audio(currentAudioUrl);

                audio.ontimeupdate = () => {
                    if (audio && audio.duration > 0) {
                        const progress = (audio.currentTime / audio.duration) * 100;
                        progressFill.style.width = `${Math.min(progress, 100)}%`;
                    }
                };

                audio.onended = () => {
                    isPlaying = false;
                    playBtn.textContent = '▶️';
                    progressFill.style.width = '0%';
                    clearInterval(progressInterval);
                    if (currentAudioUrl) {
                        URL.revokeObjectURL(currentAudioUrl);
                        currentAudioUrl = null;
                    }
                };

                audio.onerror = (e) => {
                    console.error('Audio error:', e);
                    showToast('Failed to play voice message', true);
                    isPlaying = false;
                    playBtn.textContent = '▶️';
                    clearInterval(progressInterval);
                };

                await audio.play();
                isPlaying = true;
                playBtn.textContent = '⏸️';
                showToast('▶️ Playing...');

            } catch (e) {
                console.error('Audio play error:', e);
                showToast('Failed to play voice message', true);
            }
        });
    });

    container.scrollTop = container.scrollHeight;
}

// ============ EDIT MESSAGE ============
async function editMessage(messageId, currentText, isEncrypted) {
    editingMessageId = messageId;
    editingMessageIsEncrypted = isEncrypted;
    document.getElementById('editMessageText').value = currentText;
    showModal('editMessageModal');
}

async function saveEditedMsg() {
    const newText = document.getElementById('editMessageText').value.trim();
    if (!newText || !editingMessageId) {
        closeModals();
        return;
    }

    try {
        if (editingMessageIsEncrypted) {
            let sessionKey = sessionKeys.get(currentChat.id);
            if (!sessionKey) {
                sessionKey = await getSessionKeyForChat(currentChat.id);
            }
            if (!sessionKey) {
                showToast("🔒 No encryption key for this chat", true);
                return;
            }

            const { encryptedData, iv } = await encryptForUsers(newText, sessionKey);

            const response = await fetch(`${API_BASE}/Message/${editingMessageId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    messageId: editingMessageId,
                    encryptedData: encryptedData,
                    iv: iv
                })
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(error || 'Edit failed');
            }
        } else {
            const response = await fetch(`${API_BASE}/Message/${editingMessageId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    messageId: editingMessageId,
                    messageText: newText
                })
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(error || 'Edit failed');
            }
        }

        closeModals();
        editingMessageId = null;
        await loadMessages(currentChat.id);
        await loadChats();
        showToast('✅ Message edited');

    } catch (error) {
        console.error('Edit error:', error);
        showToast('Failed to edit: ' + error.message, true);
    }
}

// ============ ГОЛОСОВАЯ ЗАПИСЬ (TOGGLE РЕЖИМ) ============
async function toggleVoiceRecording() {
    if (!currentChat) {
        showToast("Select a chat first", true);
        return;
    }

    // Если уже записываем — останавливаем
    if (isRecording) {
        stopVoiceRecording();
        return;
    }

    // Иначе начинаем запись
    await startVoiceRecording();
}

async function startVoiceRecording() {
    try {
        audioStream = await navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        let mimeType = 'audio/webm;codecs=opus';
        const codecs = [
            'audio/webm;codecs=opus',
            'audio/webm;codecs=pcm',
            'audio/webm',
            'audio/mp4',
            'audio/ogg;codecs=opus',
            'audio/ogg;codecs=vorbis'
        ];

        let supported = false;
        for (const codec of codecs) {
            if (MediaRecorder.isTypeSupported(codec)) {
                mimeType = codec;
                supported = true;
                break;
            }
        }

        if (!supported) {
            showToast('Voice recording not supported in this browser', true);
            if (audioStream) {
                audioStream.getTracks().forEach(track => track.stop());
                audioStream = null;
            }
            return;
        }

        mediaRecorder = new MediaRecorder(audioStream, {
            mimeType: mimeType,
            audioBitsPerSecond: 128000
        });

        audioChunks = [];
        recordingSeconds = 0;
        isRecording = true;

        mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                audioChunks.push(event.data);
            }
        };

        mediaRecorder.onstop = async () => {
            isRecording = false;
            clearInterval(recordingTimer);

            document.getElementById('voiceRecordingIndicator')?.classList.remove('active');
            document.getElementById('voiceBtn')?.classList.remove('recording');
            document.getElementById('voiceBtn').textContent = '🎤';

            if (audioChunks.length > 0 && recordingSeconds >= 1) {
                const mime = mimeType || 'audio/webm';
                const audioBlob = new Blob(audioChunks, { type: mime });
                await sendVoiceMessage(audioBlob);
            } else {
                if (recordingSeconds > 0) {
                    showToast('Recording too short (minimum 1 second)', true);
                }
            }

            if (audioStream) {
                audioStream.getTracks().forEach(track => track.stop());
                audioStream = null;
            }
            mediaRecorder = null;
        };

        mediaRecorder.start(100);

        recordingTimer = setInterval(() => {
            if (isRecording) {
                recordingSeconds++;
                document.getElementById('voiceTimer').textContent = formatTime(recordingSeconds);
            }
        }, 1000);

        document.getElementById('voiceRecordingIndicator').classList.add('active');
        document.getElementById('voiceBtn').classList.add('recording');
        document.getElementById('voiceBtn').textContent = '⏹️';
        document.getElementById('voiceTimer').textContent = '00:00';

        if (navigator.vibrate) navigator.vibrate(50);

        showToast('🎤 Recording... Press again to stop');

    } catch (e) {
        console.error('Recording error:', e);
        showToast('Cannot access microphone. Please allow microphone access.', true);
        isRecording = false;
        if (audioStream) {
            audioStream.getTracks().forEach(track => track.stop());
            audioStream = null;
        }
    }
}

function stopVoiceRecording() {
    if (mediaRecorder && isRecording) {
        try {
            mediaRecorder.stop();
            showToast('⏹️ Stopping...');
        } catch (e) {
            console.warn('Stop recording error:', e);
        }
    }
}

function cancelVoiceRecording() {
    if (mediaRecorder && isRecording) {
        try {
            mediaRecorder.stop();
        } catch (e) {
            console.warn('Cancel recording error:', e);
        }
        audioChunks = [];
        recordingSeconds = 0;
        isRecording = false;
        clearInterval(recordingTimer);
        document.getElementById('voiceRecordingIndicator').classList.remove('active');
        document.getElementById('voiceBtn').classList.remove('recording');
        document.getElementById('voiceBtn').textContent = '🎤';
        document.getElementById('voiceTimer').textContent = '00:00';
        if (audioStream) {
            audioStream.getTracks().forEach(track => track.stop());
            audioStream = null;
        }
        mediaRecorder = null;
        showToast('Recording cancelled');
    }
}

async function sendVoiceMessage(blob) {
    const fd = new FormData();
    const fileExtension = blob.type.includes('mp4') ? 'mp4' : 'webm';
    fd.append('file', blob, `voice.${fileExtension}`);
    fd.append('chatId', currentChat.id);
    fd.append('isVoice', 'true');
    fd.append('duration', String(recordingSeconds));

    showToast('⏳ Sending voice message...');

    try {
        const res = await fetch(`${API_BASE}/File/upload`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` },
            body: fd
        });

        if (res.ok) {
            const data = await res.json();
            console.log('✅ Voice uploaded:', data);

            await loadMessages(currentChat.id);
            await loadChats();

            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke('SendMessage', currentChat.id, currentUser.id, currentUser.name, `🎤 Voice message (${recordingSeconds}s)`);
                } catch (e) {
                    console.warn("SignalR notify failed:", e);
                }
            }

            showToast('✅ Voice message sent!');
        } else {
            const error = await res.text();
            showToast('Failed to send voice: ' + error, true);
        }
    } catch (e) {
        console.error('Voice send error:', e);
        showToast('Failed to send voice message', true);
    }
}

// ============ SEND MESSAGE ============
async function sendMessage() {
    const input = document.getElementById('messageInput');
    const text = input.value.trim();

    if (!text || !currentChat) return;

    if (!serverPublicKey) {
        await loadServerPublicKey();
    }

    let sessionKey = sessionKeys.get(currentChat.id);
    if (!sessionKey) {
        sessionKey = await getSessionKeyForChat(currentChat.id);
    }
    if (!sessionKey) {
        showToast("🔒 No encryption key for this chat", true);
        return;
    }

    try {
        const { encryptedData: encryptedForUsers, iv: ivForUsers } = await encryptForUsers(text, sessionKey);

        let encryptedForServer = "";
        let ivForServer = "";

        if (serverPublicKey) {
            try {
                const serverEncrypted = await encryptForServer(text, serverPublicKey);
                encryptedForServer = serverEncrypted.encryptedData;
                ivForServer = serverEncrypted.iv;
            } catch (e) {
                console.warn("Server encrypt failed:", e);
            }
        }

        const res = await fetch(`${API_BASE}/Message/dual-encrypted`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({
                encryptedForUsers: encryptedForUsers,
                ivForUsers: ivForUsers,
                encryptedForServer: encryptedForServer || "",
                ivForServer: ivForServer || "",
                userId: currentUser.id,
                chatId: currentChat.id
            })
        });

        if (!res.ok) {
            const error = await res.text();
            throw new Error(error);
        }

        input.value = '';
        await loadMessages(currentChat.id);
        await loadChats();
        unreadCounts.delete(currentChat.id);
        updateUnreadBadge(currentChat.id, 0);

        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            try {
                await connection.invoke('SendEncryptedMessage', currentChat.id, currentUser.id, currentUser.name, encryptedForUsers, ivForUsers);
            } catch (e) {
                console.warn("SignalR notify failed:", e);
            }
        }
    } catch (e) {
        console.error('Send error:', e);
        showToast('Failed to send: ' + e.message, true);
    }
}

// ============ FILE UPLOAD ============
async function uploadFile() {
    const input = document.getElementById('fileInput');
    const file = input.files[0];
    if (!file || !currentChat) return;

    if (file.size > 50 * 1024 * 1024) {
        showToast('Max 50MB', true);
        return;
    }

    const fd = new FormData();
    fd.append('file', file);
    fd.append('chatId', currentChat.id);

    showToast('⏳ Uploading...');

    try {
        const res = await fetch(`${API_BASE}/File/upload`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` },
            body: fd
        });

        if (res.ok) {
            const data = await res.json();
            console.log('📁 File uploaded:', data);

            input.value = '';
            await loadMessages(currentChat.id);
            await loadChats();

            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await connection.invoke('SendMessage', currentChat.id, currentUser.id, currentUser.name, `📎 ${file.name}`);
                } catch (e) {
                    console.warn("SignalR notify failed:", e);
                }
            }

            showToast('✅ File uploaded!');
        } else {
            const error = await res.text();
            showToast('Upload failed: ' + error, true);
        }
    } catch (e) {
        console.error('Upload error:', e);
        showToast('Upload error: ' + e.message, true);
    }
}

async function downloadFile(messageId, fileName) {
    if (!messageId) return showToast('No file ID', true);
    showToast('⏳ Downloading...');
    try {
        const res = await fetch(`${API_BASE}/File/download/${messageId}`, { headers: { 'Authorization': `Bearer ${token}` } });
        if (res.ok) {
            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName || 'file';
            document.body.appendChild(a);
            a.click();
            setTimeout(() => { URL.revokeObjectURL(url); a.remove(); }, 100);
            showToast('✅ Downloaded!');
        } else showToast('File not found', true);
    } catch (e) { showToast('Download failed', true); }
}

async function deleteMessage(messageId) {
    if (!confirm('Delete this message? It will go to trash bin and can be restored.')) return;
    try {
        const res = await api('/Message/' + messageId, { method: 'DELETE' });
        if (res.ok) {
            await loadMessages(currentChat.id);
            await loadChats();
            showToast('✅ Message moved to trash');
        } else {
            showToast('Failed to delete', true);
        }
    } catch (e) {
        console.error('Delete error:', e);
        showToast('Failed to delete: ' + e.message, true);
    }
}

// ============ SEARCH ============
async function searchUsers() { const q = document.getElementById('searchUserInput').value.trim(); const div = document.getElementById('searchResults'); if (q.length < 2) { div.innerHTML = ''; return; } try { const res = await api('/User/all'); const users = await res.json(); const filtered = users.filter(u => u.name.toLowerCase().includes(q.toLowerCase()) && u.id !== currentUser.id); if (!filtered.length) { div.innerHTML = '<div class="empty-state">👤 No users found</div>'; return; } div.innerHTML = filtered.map(u => `<div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}"><img src="${API_BASE}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'"><div><strong>${escapeHtml(u.name)}</strong></div></div>`).join(''); document.querySelectorAll('#searchResults .user-search-result').forEach(el => el.addEventListener('click', () => createPrivateChat(el.dataset.id, el.dataset.name))); } catch (e) { div.innerHTML = '<div class="empty-state">Error</div>'; } }

function showCreateGroup() { selectedGroupMembers = []; updateSelected(); document.getElementById('groupName').value = ''; document.getElementById('groupMaxUsers').value = '100'; document.getElementById('groupSearchInput').value = ''; document.getElementById('groupSearchResults').innerHTML = ''; showModal('createGroupModal'); }
async function searchGroupUsers() { const q = document.getElementById('groupSearchInput').value.trim(); const div = document.getElementById('groupSearchResults'); if (q.length < 2) { div.innerHTML = ''; return; } try { const res = await api('/User/all'); const users = await res.json(); const filtered = users.filter(u => u.name.toLowerCase().includes(q.toLowerCase()) && u.id !== currentUser.id && !selectedGroupMembers.some(m => m.id === u.id)); div.innerHTML = filtered.map(u => `<div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}"><img src="${API_BASE}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'"><div>${escapeHtml(u.name)}</div></div>`).join(''); document.querySelectorAll('#groupSearchResults .user-search-result').forEach(el => el.addEventListener('click', () => { selectedGroupMembers.push({ id: el.dataset.id, name: el.dataset.name }); updateSelected(); document.getElementById('groupSearchInput').value = ''; div.innerHTML = ''; })); } catch (e) { div.innerHTML = '<div class="empty-state">Error</div>'; } }
function updateSelected() { const div = document.getElementById('selectedMembers'); if (!selectedGroupMembers.length) { div.innerHTML = '<div class="empty-state">👥 No members selected</div>'; return; } div.innerHTML = selectedGroupMembers.map(m => `<div class="member-tag">${escapeHtml(m.name)}<button data-id="${m.id}">×</button></div>`).join(''); document.querySelectorAll('#selectedMembers .member-tag button').forEach(btn => btn.addEventListener('click', () => { selectedGroupMembers = selectedGroupMembers.filter(m => m.id !== btn.dataset.id); updateSelected(); })); }
async function createGroup() { const name = document.getElementById('groupName').value.trim(); const max = parseInt(document.getElementById('groupMaxUsers').value); if (!name) return showToast('Enter group name', true); if (!selectedGroupMembers.length) return showToast('Select at least one member', true); const ids = [currentUser.id, ...selectedGroupMembers.map(m => m.id)]; try { const res = await api('/Chat', { method: 'POST', body: JSON.stringify({ memberIds: ids, maxUsers: max, chatName: name }) }); if (res.ok) { const chat = await res.json(); closeModals(); await loadChats(); await selectChat(chat.id, chat.chatName); showToast(`✨ Group "${name}" created`); } } catch (e) { showToast('Error creating group', true); } }

async function showEditChatModal() {
    if (!currentChatInfo) return;
    const isGroup = currentChatInfo.maxUsers > 2 || (currentChatInfo.users?.length > 2);
    document.getElementById('editChatTitle').innerText = isGroup ? '✏️ Edit Group' : '✏️ Edit Chat';
    document.getElementById('editChatName').value = currentChatInfo.chatName;
    document.getElementById('leaveDeleteBtn').innerText = isGroup ? '🚪 Leave Group' : '🗑️ Delete Chat';
    if (isGroup) {
        document.getElementById('groupAvatarSection').style.display = 'block';
        document.getElementById('groupMembersSection').style.display = 'block';
        const avatarUrl = await getGroupAvatarUrl(currentChat.id);
        document.getElementById('groupAvatarPreview').src = avatarUrl;
        await renderMembers();
    } else {
        document.getElementById('groupAvatarSection').style.display = 'none';
        document.getElementById('groupMembersSection').style.display = 'none';
    }
    showModal('editChatModal');
}

async function renderMembers() { const members = currentChatInfo.users || []; const container = document.getElementById('membersList'); container.innerHTML = members.map(m => `<div class="member-item"><div><img src="${API_BASE}/User/avatar/${m.id}?access_token=${getSafeToken()}" style="width:32px;height:32px;border-radius:50%;object-fit:cover"> <strong>${escapeHtml(m.name)}</strong>${m.id === currentChatInfo.createdById ? ' 👑' : ''}</div>${m.id !== currentUser.id && currentChatInfo.createdById === currentUser.id ? `<button class="remove-member" data-id="${m.id}" data-name="${escapeHtml(m.name)}">Remove</button>` : ''}</div>`).join(''); document.querySelectorAll('.remove-member').forEach(btn => btn.addEventListener('click', () => removeMember(btn.dataset.id, btn.dataset.name))); }
async function searchAddMember() { const q = document.getElementById('addMemberSearch').value.trim(); const div = document.getElementById('addMemberResults'); if (q.length < 2) { div.innerHTML = ''; return; } try { const res = await api('/User/all'); const users = await res.json(); const existing = currentChatInfo.users.map(u => u.id); const filtered = users.filter(u => u.name.toLowerCase().includes(q.toLowerCase()) && u.id !== currentUser.id && !existing.includes(u.id)); div.innerHTML = filtered.map(u => `<div class="user-search-result" data-id="${u.id}" data-name="${escapeHtml(u.name)}"><img src="${API_BASE}/User/avatar/${u.id}?access_token=${getSafeToken()}" onerror="this.src='${DEFAULT_AVATAR}'"><div>${escapeHtml(u.name)}</div></div>`).join(''); document.querySelectorAll('#addMemberResults .user-search-result').forEach(el => el.addEventListener('click', () => addMemberToGroup(el.dataset.id, el.dataset.name))); } catch (e) { div.innerHTML = '<div class="empty-state">Error</div>'; } }
async function addMemberToGroup(uid, uname) { try { await api('/Chat/add-user', { method: 'POST', body: JSON.stringify({ chatId: currentChat.id, userId: uid }) }); currentChatInfo = await getChatInfo(currentChat.id); await renderMembers(); document.getElementById('addMemberSearch').value = ''; document.getElementById('addMemberResults').innerHTML = ''; showToast(`✅ ${uname} added`); } catch (e) { showToast('Error', true); } }
async function removeMember(uid, uname) { if (!confirm(`Remove ${uname}?`)) return; try { await api('/Chat/remove-user', { method: 'POST', body: JSON.stringify({ chatId: currentChat.id, userId: uid }) }); currentChatInfo = await getChatInfo(currentChat.id); if (currentChatInfo) { await renderMembers(); showToast(`✅ ${uname} removed`); } else { closeModals(); currentChat = null; currentChatInfo = null; document.getElementById('messageInput').disabled = true; document.getElementById('sendBtn').disabled = true; await loadChats(); } } catch (e) { showToast('Error', true); } }
async function saveChatEdit() { const newName = document.getElementById('editChatName').value.trim(); if (newName && newName !== currentChatInfo.chatName) { try { await api('/Chat/' + currentChat.id, { method: 'PUT', body: JSON.stringify({ chatName: newName }) }); currentChatInfo.chatName = newName; await loadChats(); showToast('✅ Renamed'); } catch (e) { showToast('Error', true); } } closeModals(); }
function leaveOrDelete() { const isGroup = currentChatInfo.maxUsers > 2 || currentChatInfo.users?.length > 2; if (isGroup) leaveGroup(); else deleteChat(); }
async function leaveGroup() { if (!confirm('Leave group?')) return; try { await api('/Chat/remove-user', { method: 'POST', body: JSON.stringify({ chatId: currentChat.id, userId: currentUser.id }) }); closeModals(); currentChat = null; currentChatInfo = null; document.getElementById('messageInput').disabled = true; document.getElementById('sendBtn').disabled = true; await loadChats(); showToast('✅ Left group'); } catch (e) { showToast('Error', true); } }
async function deleteChat() { if (!confirm('Delete chat?')) return; try { await api('/Chat/' + currentChat.id, { method: 'DELETE' }); closeModals(); currentChat = null; currentChatInfo = null; document.getElementById('messageInput').disabled = true; document.getElementById('sendBtn').disabled = true; await loadChats(); showToast('✅ Chat deleted'); } catch (e) { showToast('Error', true); } }

// ============ SIGNALR ============
async function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/messengerHub", { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveEncryptedMessage", async (userId, userName, encryptedData, iv, chatId) => {
        console.log(`[SignalR] ReceiveEncryptedMessage from ${userName} in chat ${chatId}`);
        if (currentChat && currentChat.id === chatId) {
            await loadMessages(currentChat.id);
            await markChatAsRead(chatId);
        } else {
            const currentCount = unreadCounts.get(chatId) || 0;
            updateUnreadBadge(chatId, currentCount + 1);
            await loadChats();
            showToast(`🔒 New encrypted message from ${userName}`);
        }
    });

    connection.on("ReceiveMessage", async (userId, userName, messageText, chatId) => {
        if (currentChat && currentChat.id === chatId) {
            await loadMessages(currentChat.id);
            await markChatAsRead(chatId);
        } else {
            const currentCount = unreadCounts.get(chatId) || 0;
            updateUnreadBadge(chatId, currentCount + 1);
            await loadChats();
            showToast(`💬 ${userName}: ${messageText?.substring(0, 40)}`);
        }
    });

    connection.on("NewFileUploaded", async (fileData) => {
        console.log("📁 New file uploaded via SignalR:", fileData);
        if (currentChat && currentChat.id === fileData.chatId) {
            await loadMessages(currentChat.id);
            await loadChats();
        }
    });

    connection.on("NewVoiceMessage", async (userId, userName, duration, chatId) => {
        console.log("🎤 New voice message from:", userName);
        if (currentChat && currentChat.id === chatId) {
            await loadMessages(currentChat.id);
            await loadChats();
        }
    });

    connection.on("UserOnline", (userId, isOnline) => { onlineUsers.set(userId, isOnline); loadChats(); });
    connection.on("UserTyping", (userId, name) => { if (currentChat && userId !== currentUser?.id) { const div = document.getElementById('typingIndicator'); div.innerText = `${name} is typing...`; div.style.display = 'block'; clearTimeout(typingTimeout); typingTimeout = setTimeout(() => div.style.display = 'none', 2000); } });
    connection.on("UserStoppedTyping", () => { document.getElementById('typingIndicator').style.display = 'none'; });
    connection.on("NewChatCreated", async (chat) => { console.log("NewChatCreated:", chat); await loadChats(); });
    connection.on("UserProfileUpdated", (userId, newName) => { if (currentUser && currentUser.id === userId) { currentUser.name = newName; document.getElementById('currentUserName').innerText = newName; } loadChats(); });
    connection.on("UserAvatarUpdated", (userId) => { const avatarUrl = `${API_BASE}/User/avatar/${userId}?t=${Date.now()}`; if (currentUser && currentUser.id === userId) { document.getElementById('sidebarAvatar').src = avatarUrl; document.getElementById('profileAvatar').src = avatarUrl; } loadChats(); });

    try {
        await connection.start();
        console.log("SignalR connected");
        const chatsRes = await api(`/Chat/user-chats/${currentUser.id}`);
        const chats = await chatsRes.json();
        for (const chat of chats) {
            await connection.invoke('JoinChat', chat.id, currentUser.id, currentUser.name);
        }
        if (currentChat) {
            await connection.invoke('JoinChat', currentChat.id, currentUser.id, currentUser.name);
        }
    } catch (e) { console.error("SignalR error:", e); }
}

function startTyping() {
    if (connection && connection.state === signalR.HubConnectionState.Connected && currentChat && document.getElementById('messageInput').value.length > 0) {
        connection.invoke('UserIsTyping', currentChat.id, currentUser.id, currentUser.name);
        clearTimeout(typingTimeout);
        typingTimeout = setTimeout(() => {
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke('UserStoppedTyping', currentChat.id, currentUser.id);
            }
        }, 1000);
    }
}

async function initApp() {
    if (!token) {
        const t = localStorage.getItem('token');
        const uid = localStorage.getItem('userId');
        if (t && uid) {
            token = t;
            currentUser = { id: uid };
            const privateKeyBase64 = await loadPrivateKeyFromDB();
            if (privateKeyBase64) {
                privateKey = await importPrivateKey(privateKeyBase64);
            }
        } else return;
    }
    document.getElementById('authContainer').style.display = 'none';
    document.getElementById('appContainer').classList.add('active');
    await loadUser();
    if (currentUser && !currentUser.publicKey) {
        const profileRes = await api('/User/profile');
        const profile = await profileRes.json();
        currentUser.publicKey = profile.publicKey;
        currentUser.name = profile.name;
    }
    await loadAllSessionKeysFromDB();
    await loadUnreadCounts();
    await loadChats();
    await loadServerPublicKey();
    await initSignalR();
    setTimeout(() => { if (typeof window.initEmojiPicker === 'function') window.initEmojiPicker(); }, 500);
}

function logout() {
    localStorage.clear();
    token = null;
    currentUser = null;
    currentChat = null;
    privateKey = null;
    sessionKeys.clear();
    groupAvatarCache.clear();
    if (connection) connection.stop();
    document.getElementById('authContainer').style.display = 'flex';
    document.getElementById('appContainer').classList.remove('active');
}

// ============ EVENT BINDINGS ============
document.getElementById('doLoginBtn')?.addEventListener('click', handleLogin);
document.getElementById('sendCodeBtn')?.addEventListener('click', requestCode);
document.getElementById('verifyBtn')?.addEventListener('click', verifyReg);
document.getElementById('backToRegisterBtn')?.addEventListener('click', () => { document.getElementById('verifyForm').style.display = 'none'; document.getElementById('registerForm').style.display = 'flex'; });
document.querySelectorAll('.auth-tab').forEach(t => t.addEventListener('click', () => switchTab(t.dataset.tab)));
document.getElementById('logoutBtn')?.addEventListener('click', logout);
document.getElementById('menuBtn')?.addEventListener('click', () => document.getElementById('sidebar').classList.toggle('open'));
document.getElementById('newChatBtn')?.addEventListener('click', () => { document.getElementById('searchUserInput').value = ''; document.getElementById('searchResults').innerHTML = ''; showModal('newChatModal'); });
document.getElementById('createGroupBtn')?.addEventListener('click', showCreateGroup);
document.getElementById('trashBtn')?.addEventListener('click', showTrashBin);
document.getElementById('refreshTrashBtn')?.addEventListener('click', loadDeletedMessages);
document.getElementById('closeTrashBtn')?.addEventListener('click', closeModals);
document.getElementById('attachBtn')?.addEventListener('click', () => document.getElementById('fileInput').click());
document.getElementById('fileInput')?.addEventListener('change', uploadFile);
document.getElementById('sendBtn')?.addEventListener('click', sendMessage);
document.getElementById('messageInput')?.addEventListener('input', startTyping);
document.getElementById('messageInput')?.addEventListener('keypress', (e) => { if (e.key === 'Enter') sendMessage(); });
document.getElementById('searchUserInput')?.addEventListener('input', searchUsers);
document.getElementById('groupSearchInput')?.addEventListener('input', searchGroupUsers);
document.getElementById('createGroupSubmit')?.addEventListener('click', createGroup);
document.getElementById('addMemberSearch')?.addEventListener('input', searchAddMember);
document.getElementById('saveChatEdit')?.addEventListener('click', saveChatEdit);
document.getElementById('leaveDeleteBtn')?.addEventListener('click', leaveOrDelete);
document.getElementById('sidebarAvatar')?.addEventListener('click', showProfileModal);
document.getElementById('currentUserName')?.addEventListener('click', showProfileModal);
document.getElementById('updateProfileBtn')?.addEventListener('click', updateProfile);
document.getElementById('removeAvatarBtn')?.addEventListener('click', deleteAvatar);
document.getElementById('profileAvatar')?.addEventListener('click', () => document.getElementById('avatarFile').click());
document.getElementById('avatarFile')?.addEventListener('change', (e) => uploadAvatar(e.target.files[0]));
document.getElementById('saveEditMessage')?.addEventListener('click', saveEditedMsg);
document.getElementById('uploadGroupAvatarBtn')?.addEventListener('click', () => document.getElementById('groupAvatarFile').click());
document.getElementById('groupAvatarFile')?.addEventListener('change', (e) => uploadGroupAvatar(e.target.files[0]));
document.getElementById('deleteGroupAvatarBtn')?.addEventListener('click', deleteGroupAvatar);
document.querySelectorAll('.close-modal').forEach(btn => btn.addEventListener('click', closeModals));

// ===== ГОЛОСОВАЯ КНОПКА (TOGGLE) =====
document.getElementById('voiceBtn')?.addEventListener('click', toggleVoiceRecording);
document.getElementById('cancelVoiceBtn')?.addEventListener('click', cancelVoiceRecording);

if (localStorage.getItem('token')) initApp();