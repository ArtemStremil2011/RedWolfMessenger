# 📚 Документация для разработчика (бэкенд + фронтенд)

---

## 🎯 Введение

Этот документ описывает **все основные методы** в проекте Red Wolf Messenger. Ты можешь брать задачи по багам и доработкам, не вникая во всю архитектуру. Просто найди нужный метод по названию и смотри, что он делает.

---

## 📁 Структура проекта

```
Messenger/
├── Controllers/           # API (входные точки)
├── Services/              # Бизнес-логика (основной код)
├── Models/                # Модели данных
├── DTOs/                  # Объекты передачи данных
├── Hubs/                  # SignalR (реалтайм)
└── wwwroot/
    └── index.html         # Фронтенд
```

---

## 🖥️ БЭКЕНД (C#)

### 📂 Controllers/ — входные точки API

| Файл | Метод | URL | Что делает |
|------|-------|-----|-------------|
| **UserController.cs** | `Login` | POST `/api/User/login` | Вход по телефону или имени |
| | `RequestVerification` | POST `/api/User/request-verification` | Запрос SMS кода |
| | `VerifyAndRegister` | POST `/api/User/verify-and-register` | Подтверждение кода и регистрация |
| | `GetProfile` | GET `/api/User/profile` | Получить свой профиль |
| | `UpdateProfile` | PUT `/api/User/update-profile` | Обновить имя или пароль |
| | `UploadAvatar` | POST `/api/User/upload-avatar` | Загрузить аватарку |
| | `DeleteAvatar` | DELETE `/api/User/avatar` | Удалить аватарку |
| | `GetAvatar` | GET `/api/User/avatar/{userId}` | Получить аватарку пользователя |
| **ChatController.cs** | `GetUserChats` | GET `/api/Chat/user-chats/{userId}` | Получить все чаты пользователя |
| | `GetChat` | GET `/api/Chat/{chatId}` | Получить информацию о чате |
| | `GetChatMessages` | GET `/api/Chat/{chatId}/messages` | Получить сообщения чата (с пагинацией) |
| | `CreateChat` | POST `/api/Chat` | Создать чат (личный или группу) |
| | `UpdateChatName` | PUT `/api/Chat/{chatId}` | Переименовать чат |
| | `AddUserToChat` | POST `/api/Chat/add-user` | Добавить участника в группу |
| | `RemoveUserFromChat` | POST `/api/Chat/remove-user` | Удалить участника из группы |
| | `DeleteChat` | DELETE `/api/Chat/{id}` | Удалить чат |
| | `UploadGroupAvatar` | POST `/api/Chat/{chatId}/avatar` | Загрузить аватарку группы |
| | `GetGroupAvatar` | GET `/api/Chat/{chatId}/avatar` | Получить аватарку группы |
| | `DeleteGroupAvatar` | DELETE `/api/Chat/{chatId}/avatar` | Удалить аватарку группы |
| **MessageController.cs** | `Create` | POST `/api/Message` | Отправить сообщение |
| | `Update` | PUT `/api/Message/{id}` | Редактировать сообщение |
| | `Delete` | DELETE `/api/Message/{id}` | Удалить сообщение |
| **FileController.cs** | `UploadFile` | POST `/api/File/upload` | Загрузить файл |
| | `DownloadFile` | GET `/api/File/download/{messageId}` | Скачать файл |
| | `DeleteFile` | DELETE `/api/File/{messageId}` | Удалить файл |

---

### 📂 Services/ — бизнес-логика

#### UserReadService.cs (чтение пользователей)

| Метод | Что делает |
|-------|------------|
| `GetProfileAsync(userId)` | Получить профиль пользователя по ID |
| `GetUserByIdAsync(userId, currentUserId)` | Получить пользователя (с проверкой доступа) |
| `GetAllUsersAsync()` | Получить всех пользователей (только для админа) |
| `UserExistsAsync(userId)` | Проверить, существует ли пользователь |

#### UserWriteService.cs (запись пользователей)

| Метод | Что делает |
|-------|------------|
| `LoginAsync(loginDto)` | Аутентификация, возвращает токен |
| `RequestVerificationCodeAsync(registerDto)` | Генерация SMS кода |
| `VerifyAndRegisterAsync(phoneNumber, code)` | Подтверждение кода и создание пользователя |
| `UpdateProfileAsync(userId, updateDto)` | Обновление имени или пароля |
| `UploadAvatarAsync(userId, file)` | Загрузка аватарки, сохранение на диск |
| `DeleteAvatarAsync(userId)` | Удаление аватарки |

#### ChatReadService.cs (чтение чатов)

| Метод | Что делает |
|-------|------------|
| `GetUserChatsAsync(userId, currentUserId)` | Получить все чаты пользователя |
| `GetChatAsync(chatId, currentUserId)` | Получить информацию о чате |
| `GetChatMessagesAsync(chatId, currentUserId, page, pageSize)` | Получить сообщения чата (текстовые + файловые) |
| `GetTotalMessagesCountAsync(chatId)` | Количество сообщений в чате |
| `UserInChatAsync(chatId, userId)` | Проверить, есть ли пользователь в чате |
| `GetGroupAvatarPathAsync(chatId, currentUserId)` | Получить путь к аватарке группы |

#### ChatWriteService.cs (запись чатов)

| Метод | Что делает |
|-------|------------|
| `CreateChatAsync(dto, currentUserId)` | Создать чат (личный или группу) |
| `UpdateChatNameAsync(chatId, newName, currentUserId)` | Переименовать чат |
| `AddUserToChatAsync(chatId, userIdToAdd, currentUserId)` | Добавить участника в группу |
| `RemoveUserFromChatAsync(chatId, userIdToRemove, currentUserId)` | Удалить участника из группы |
| `DeleteChatAsync(chatId, currentUserId)` | Удалить чат |
| `LeaveGroupAsync(chatId, currentUserId)` | Выйти из группы |
| `UploadGroupAvatarAsync(chatId, file, currentUserId)` | Загрузить аватарку группы |
| `DeleteGroupAvatarAsync(chatId, currentUserId)` | Удалить аватарку группы |

#### MessageReadService.cs (чтение сообщений)

| Метод | Что делает |
|-------|------------|
| `GetMessageByIdAsync(messageId, currentUserId)` | Получить сообщение по ID (с проверкой доступа) |
| `GetChatMessagesAsync(chatId, currentUserId, page, pageSize)` | Получить сообщения чата |

#### MessageWriteService.cs (запись сообщений)

| Метод | Что делает |
|-------|------------|
| `CreateMessageAsync(userId, chatId, text)` | Создать новое сообщение |
| `UpdateMessageAsync(messageId, newText, currentUserId)` | Редактировать сообщение |
| `DeleteMessageAsync(messageId, currentUserId)` | Мягкое удаление (IsDeleted = true) |

#### FileReadService.cs (чтение файлов)

| Метод | Что делает |
|-------|------------|
| `GetFileMessageAsync(messageId, currentUserId)` | Получить информацию о файловом сообщении |
| `GetChatFilesAsync(chatId, currentUserId)` | Получить все файлы чата |
| `UserHasAccessToFileAsync(messageId, currentUserId)` | Проверить, есть ли у пользователя доступ к файлу |

#### FileWriteService.cs (запись файлов)

| Метод | Что делает |
|-------|------------|
| `UploadFileAsync(chatId, file, caption, currentUserId)` | Сохранить файл на диск и создать FileMessage |
| `DeleteFileAsync(messageId, currentUserId)` | Удалить файл с диска и из БД |

---

### 📂 Hubs/MessengerHub.cs — SignalR

| Метод | Когда вызывается | Что делает |
|-------|------------------|------------|
| `OnConnectedAsync()` | При подключении клиента | Добавляет пользователя в список онлайн |
| `OnDisconnectedAsync()` | При отключении клиента | Удаляет пользователя из списка онлайн |
| `JoinChat(chatId, userId, userName)` | Когда пользователь открывает чат | Добавляет соединение в группу чата |
| `SendMessage(chatId, userId, userName, messageText)` | Когда отправляется сообщение | Рассылает сообщение всем в группе чата |
| `UserIsTyping(chatId, userId, userName)` | Когда пользователь печатает | Рассылает уведомление "печатает..." |
| `UserStoppedTyping(chatId, userId)` | Когда пользователь перестал печатать | Убирает индикатор печати |

---

### 📂 Models/ — структуры данных

| Класс | Таблица в БД | Поля |
|-------|--------------|------|
| `User` | Users | Id, Name, PhoneNumber, PasswordHash, AvatarPath, RegisterDate, IsPhoneNumberConfirmed, Role |
| `Chat` | Chats | Id, ChatName, MaxUsers, IsPrivate, CreatedAt, LastActivityAt, CreatedById, AvatarPath |
| `Message` | Messages | MessageId, MessageText, MessageCreateDate, MessageLastUpdateDate, UserId, ChatId, IsDeleted, IsSystemMessage |
| `FileMessage` | FileMessages | (наследует Message) + FileName, FilePath, FileSize, ContentType |

---

## 🎨 ФРОНТЕНД (JavaScript в index.html)

### 🧠 Глобальные переменные

| Переменная | Тип | Что хранит |
|------------|-----|------------|
| `currentUser` | Object | Текущий пользователь { id, name } |
| `currentChat` | Object | Текущий чат { id, name } |
| `currentChatInfo` | Object | Детальная информация о чате |
| `token` | String | JWT токен авторизации |
| `connection` | SignalR | SignalR соединение |
| `onlineUsers` | Map | Онлайн статусы пользователей |

---

### 🔧 Основные функции

#### Авторизация

| Функция | Что делает |
|---------|------------|
| `handleLogin()` | Отправляет запрос на `/api/User/login`, сохраняет токен |
| `requestCode()` | Запрашивает SMS код (регистрация) |
| `verifyReg()` | Подтверждает код и завершает регистрацию |

#### Работа с чатами

| Функция | Что делает |
|---------|------------|
| `loadChats()` | Загружает список чатов через API и рендерит их |
| `selectChat(chatId, chatName)` | Открывает чат, загружает сообщения, подключается к SignalR группе |
| `createPrivateChat(userId, userName)` | Создаёт личный чат |
| `showCreateGroup()` | Открывает модалку создания группы |
| `createGroup()` | Создаёт группу через API |

#### Работа с сообщениями

| Функция | Что делает |
|---------|------------|
| `loadMessages(chatId)` | Загружает сообщения чата через API |
| `renderMessages(messages)` | Отрисовывает сообщения в контейнере |
| `sendMessage()` | Отправляет текст через API и SignalR |
| `editMessage(id, currentText)` | Открывает модалку редактирования |
| `saveEditedMessage()` | Сохраняет отредактированное сообщение |
| `deleteMessage(id)` | Удаляет сообщение |

#### Работа с файлами

| Функция | Что делает |
|---------|------------|
| `uploadFile()` | Загружает файл через FormData |
| `downloadFile(messageId, fileName)` | Скачивает файл через API |
| `deleteFileMessage(messageId)` | Удаляет файловое сообщение |

#### Работа с аватарками

| Функция | Что делает |
|---------|------------|
| `uploadAvatar(file)` | Загружает аватарку пользователя |
| `deleteAvatar()` | Удаляет аватарку пользователя |
| `uploadGroupAvatar(file)` | Загружает аватарку группы |
| `deleteGroupAvatar()` | Удаляет аватарку группы |
| `loadGroupAvatar(chatId, imgElement)` | Загружает аватарку группы через fetch (с токеном) |

#### Профиль

| Функция | Что делает |
|---------|------------|
| `showProfileModal()` | Открывает модалку профиля |
| `updateProfile()` | Обновляет имя или пароль |

#### Вспомогательные

| Функция | Что делает |
|---------|------------|
| `showToast(msg, isErr)` | Показывает уведомление (зелёное/красное) |
| `formatMessageDate(dateString)` | Форматирует дату сообщения (сегодня/вчера/день недели) |
| `isMessageEdited(msg)` | Проверяет, редактировалось ли сообщение (разница дат > 1 сек) |
| `getSafeToken()` | Возвращает закодированный токен для URL |
| `copyToClipboard(text)` | Копирует текст в буфер обмена |

---

### 🔌 SignalR обработчики

| Обработчик | Что делает |
|------------|------------|
| `connection.on("ReceiveMessage")` | Обновляет сообщения при получении нового |
| `connection.on("UserOnline")` | Обновляет статус онлайн пользователя |
| `connection.on("UserTyping")` | Показывает индикатор "печатает..." |
| `connection.on("UserStoppedTyping")` | Скрывает индикатор печати |

---

### 🎨 CSS классы (для вёрстки)

| Класс | Где используется | Описание |
|-------|------------------|----------|
| `.message` | Контейнер сообщения | Базовый стиль сообщения |
| `.message.own` | Своё сообщение | Выравнивание справа, красный фон |
| `.message.system` | Системное сообщение | По центру, серый курсив |
| `.message-file` | Файловое сообщение | Блок с иконкой файла |
| `.download-btn` | Кнопка скачивания | Зелёная кнопка |
| `.chat-item` | Элемент чата в списке | Блок чата |
| `.online-dot` | Индикатор онлайн | Зелёная/серая точка |
| `.modal` | Модальное окно | Затемнённый фон |
| `.toast` | Уведомление | Всплывающее уведомление |

---

## 🔍 Как найти что-то по коду

| Если нужно найти... | Ищи по... |
|---------------------|------------|
| Функцию | `function имяФункции()` |
| Обработчик события | `.addEventListener('click', ...)` |
| API запрос | `fetch(`${API_BASE}/...` |
| SignalR вызов | `connection.invoke('...` |
| CSS класс | `class="имя-класса"` |

---

