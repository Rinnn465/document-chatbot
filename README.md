# PRN222 Document Chatbot

Project sử dụng một ASP.NET Core host cho cả hai assignment và một Python service cho RAG.

## Cấu trúc

```text
document-chatbot/
├── database/                    # SQL Server schema và seed data
├── rag-service/                 # Python: chunk, embedding, retrieval và LLM
├── src/
│   ├── DocumentChatbot.Data/    # EF Core entities và DbContext
│   └── DocumentChatbot.Web/     # MVC, Razor Pages, authentication và services
└── DocumentChatbot.sln
```

Trong `DocumentChatbot.Web`:

- `Controllers` + `Views`: Assignment 1 theo MVC (mặc định của project).
- `Pages/Assignment2`: Assignment 2 theo Razor Pages.
- `Services`: authentication, documents, chat và kết nối RAG.
- `Models`: model được nhóm theo Authentication, Documents và Chat.
- `Authorization`: role và authorization policy.

Flow chat sử dụng SignalR cho kết nối realtime. Session, message và citation được lưu trong SQL Server.

## Phân quyền

| Role | MVC | Razor Pages |
|---|---|---|
| `SubjectLeader` | `/Documents` | `/Assignment2/Documents` |
| `Student` | `/Chat` | `/Assignment2/Chat` |

Tài khoản seed:

| Role | Email | Password |
|---|---|---|
| Subject Leader | `hungdt0546@fpt.edu.vn` | `12345678` |
| Student | `hungdt0546@gmail.com` | `12345678` |

## Pipeline tài liệu

Tài liệu không được chuyển thành file Markdown vật lý. ASP.NET trích xuất thành các section có cấu trúc rồi gửi sang RAG:

- PPTX: một section cho mỗi slide, giữ title, bullet, speaker notes và `slideNumber`.
- PDF: một section cho mỗi page, giữ `pageNumber`.
- DOCX: giữ các paragraph và xuống dòng trong một document section.
- Python chỉ chunk bên trong từng section, không ghép nội dung xuyên slide/page.
- Chroma lưu metadata của section để câu trả lời trích dẫn đúng tài liệu và slide/page.

Embedding mặc định là `intfloat/multilingual-e5-small`, phù hợp truy vấn tiếng Việt trên slide tiếng Anh. Khi thay parser, embedding model hoặc cấu trúc chunk, phải upload/index lại tài liệu cũ.

## Chạy project

1. Tạo SQL Server database từ thư mục gốc repository:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i database\DocumentChatbotDB.sql
```

2. Tạo file `rag-service/.env` và đặt API key chỉ ở máy chạy RAG:

```dotenv
OPENAI_API_KEY=your_openai_api_key_here
OPENAI_MODEL=gpt-5.4-mini
CHROMA_DIR=chroma_db
CHROMA_COLLECTION=course_documents
ANONYMIZED_TELEMETRY=false
KNOWLEDGE_DIR=knowledge
EMBEDDING_MODEL=intfloat/multilingual-e5-small
```

Không commit file `.env`.

3. Khởi động RAG service:

```powershell
cd rag-service
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m uvicorn main:app --reload --port 8000
```

4. Khởi động ASP.NET Core web app bằng Visual Studio hoặc lệnh:

```powershell
dotnet run --project src\DocumentChatbot.Web
```

5. Đăng nhập Subject Leader và upload lại tài liệu nếu knowledge base chưa được index bằng pipeline hiện tại.

## Knowledge snapshot

Sau khi một tài liệu được index thành công, RAG service duy trì hai lớp dữ liệu:

- `chroma_db/`: vector index dùng để tìm nội dung khi chat.
- `knowledge/`: snapshot JSON chuẩn hóa dùng để kiểm tra và dựng lại vector index.

Mỗi tài liệu có một snapshot riêng trong `knowledge/documents/`. File
`knowledge/manifest.json` chỉ lưu danh sách tài liệu, content hash và số lượng
section/chunk; nội dung của tất cả tài liệu không bị gộp vào một file lớn.
Snapshot vẫn giữ `documentId`, tên tài liệu, chapter, slide/page và chunk ID nên
log nội bộ có thể xác định chính xác nguồn được dùng. Cả `knowledge/` và
`chroma_db/` là dữ liệu runtime và không được commit.

Khi upload lại cùng một tài liệu, Chroma upsert chunk mới, xóa chunk cũ không còn
tồn tại và thay thế snapshot tương ứng. Khi xóa tài liệu, cả vector và snapshot
cũng được xóa.

Nếu Chroma bị mất nhưng snapshot còn nguyên, dựng lại index bằng:

```powershell
cd rag-service
.\.venv\Scripts\Activate.ps1
python rebuild_knowledge.py
```

Các tài liệu đã index trước khi tính năng snapshot được thêm cần được upload lại
một lần để tạo snapshot ban đầu.
