# PRN222 Document Chatbot

Web chatbot hỗ trợ sinh viên hỏi đáp dựa trên tài liệu môn PRN222. Subject Leader quản lý kho tài liệu; hệ thống tự trích xuất, chunk, embedding và lập chỉ mục để sinh câu trả lời có trích dẫn nguồn.

Project sử dụng ASP.NET Core 8 cho Web/API, SQL Server cho dữ liệu nghiệp vụ và Python/FastAPI + Chroma + OpenAI cho RAG.

## Final Group Project — 3 workflows

### Flow 1 — Upload & Manage Documents

Chỉ tài khoản `SubjectLeader` được phép quản lý tài liệu.

- Upload nhiều tài liệu PDF, DOCX và PPTX, tối đa 25 MB/tệp.
- Quản lý theo môn học và chương; bản demo sử dụng môn PRN222.
- Tự động trích xuất nội dung, chunk, embedding và lưu vector vào Chroma.
- Theo dõi realtime các bước queued, extracting, indexing, indexed hoặc failed.
- Xem danh sách tài liệu, metadata, trạng thái, số chunk và nội dung từng chunk.
- Xóa đồng bộ bản ghi SQL, vector index và knowledge snapshot.
- Có cả bản MVC và Razor Pages để đáp ứng Assignment 1/Assignment 2.

### Flow 2 — Questions & Answers

Chỉ tài khoản `Student` được sử dụng chat.

- Chat realtime qua SignalR và lưu session/message vào SQL Server.
- Dùng tối đa 8 message gần nhất để hiểu câu hỏi nối tiếp.
- Rewrite câu hỏi thành truy vấn tiếng Anh để retrieval trên tài liệu kỹ thuật.
- Chỉ trả lời bằng thông tin có trong các chunk được retrieval.
- Mỗi ý chính phải có citation; citation lưu document, chunk, page/slide và excerpt.
- Câu trả lời sử dụng ngôn ngữ của chunk được trích dẫn, không bị ép theo ngôn ngữ câu hỏi.
- Trả out-of-scope khi chunk không chứa thông tin trực tiếp; kết quả được cache đến khi knowledge base thay đổi.

Lưu ý: relevance score chỉ thể hiện độ gần của embedding, không bảo đảm chunk chứa đáp án. Log RAG có excerpt của candidate để kiểm tra nội dung thật.

### Flow 3 — Report & Statistics

Dashboard dành cho `SubjectLeader`:

- Tổng số tài liệu và tỷ lệ đã index.
- Số tài liệu processing/failed và tổng số chunks.
- Phân bố tài liệu theo loại file và chương.
- Danh sách tài liệu upload gần đây.
- Số câu hỏi theo khoảng ngày và biểu đồ câu hỏi theo ngày.
- Tự cập nhật khi trạng thái tài liệu hoặc usage chat thay đổi.

## Mapping MVC và Razor Pages

| Phần | Kiến trúc | Route chính | Role |
|---|---|---|---|
| Course/Document Management — Assignment 1 | MVC | `/Courses`, `/Documents?courseId=1` | SubjectLeader |
| Document Management — Assignment 2 | Razor Pages | `/Assignment2/Documents` | SubjectLeader |
| Upload tài liệu | Razor Pages | `/Assignment2/Documents/Upload` | SubjectLeader |
| Xem chunks | Razor Pages | `/Assignment2/Documents/{id}/Chunks` | SubjectLeader |
| Chat workspace | Razor Pages + SignalR | `/Chat` | Student |
| Reports | Razor Pages | `/Assignment2/Reports` | SubjectLeader |
| Chat REST API | MVC API | `/chat/sessions/...` | Student |
| Chat realtime | SignalR Hub | `/hubs/chat` | Student |

## Kiến trúc

```text
Browser
  └─ ASP.NET Core Web
      ├─ MVC + Razor Pages
      ├─ SignalR Hubs
      ├─ EF Core → SQL Server
      └─ HTTP → FastAPI RAG service
                  ├─ structured chunking
                  ├─ multilingual-e5 embeddings
                  ├─ Chroma vector index
                  ├─ knowledge snapshots
                  └─ OpenAI Responses API
```

```text
document-chatbot/
├── database/                    # SQL schema, indexes và seed data
├── rag-service/                 # FastAPI, retrieval, prompt và knowledge snapshot
├── scripts/                     # Docker secrets, deploy và Tailscale Serve
├── src/
│   ├── DocumentChatbot.Data/    # EF Core entities và DbContext
│   └── DocumentChatbot.Web/     # MVC, Razor Pages, SignalR và services
├── tests/                       # .NET tests
├── compose.rag.yml
└── DocumentChatbot.sln
```

## Pipeline tài liệu

ASP.NET trích xuất tài liệu thành các section có cấu trúc trước khi gửi sang RAG:

- PDF: một section cho mỗi page, giữ `pageNumber`.
- PPTX: một section cho mỗi slide, giữ title, bullet, speaker notes và `slideNumber`.
- DOCX: giữ paragraph và xuống dòng trong một document section.
- Python chỉ chunk bên trong từng section, không ghép nội dung xuyên page/slide.
- Chroma giữ `documentId`, chapter, chunk ID và page/slide để tạo citation chính xác.

Embedding mặc định là `intfloat/multilingual-e5-small`. Sau khi thay parser, embedding model hoặc cấu trúc chunk, cần upload/index lại tài liệu cũ.

## Phân quyền và tài khoản demo

| Role | Email | Password |
|---|---|---|
| Subject Leader | `hungdt0546@fpt.edu.vn` | `12345678` |
| Student | `hungdt0546@gmail.com` | `12345678` |

Authorization được kiểm tra ở cả page/controller và SignalR Hub. Student không thể truy cập trang quản lý; Subject Leader không thể sử dụng chat Student.

## Yêu cầu môi trường

- .NET SDK 8
- SQL Server LocalDB hoặc SQL Server tương thích
- Python 3.11
- OpenAI API key
- Docker Desktop và Tailscale nếu chạy RAG theo mô hình máy chủ dùng chung

## Chạy local

### 1. Khởi tạo hoặc nâng cấp database

Script SQL có tính idempotent; nên chạy lại sau mỗi lần pull code để bổ sung schema mới mà không xóa dữ liệu hiện có.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i database\DocumentChatbotDB.sql
```

### 2. Cấu hình và chạy RAG bằng Python

Tạo `rag-service/.env`:

```dotenv
OPENAI_API_KEY=your_openai_api_key_here
OPENAI_MODEL=gpt-5.4-mini
OPENAI_REASONING_EFFORT=low
CHROMA_DIR=chroma_db
CHROMA_COLLECTION=course_documents
KNOWLEDGE_DIR=knowledge
EMBEDDING_MODEL=intfloat/multilingual-e5-small
ANONYMIZED_TELEMETRY=false
```

Không commit `.env`, API key hoặc service token.

```powershell
cd rag-service
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m uvicorn main:app --reload --port 8000
```

Kiểm tra:

```powershell
Invoke-RestMethod http://127.0.0.1:8000/health
```

### 3. Chạy ASP.NET Core Web

```powershell
dotnet run --project src\DocumentChatbot.Web
```

Mặc định Web chạy tại `http://localhost:5076` và RAG tại `http://localhost:8000`.

### 4. Chuẩn bị dữ liệu demo

Đăng nhập Subject Leader, upload slide/giáo trình PRN222 thật và chờ trạng thái `Indexed`. Không nên dùng tài liệu mẫu không chứa nội dung câu hỏi vì chatbot bắt buộc trả out-of-scope khi chunk không có đáp án.

## Chạy RAG bằng Docker

Khởi tạo Docker secrets, nhập OpenAI API key bằng prompt ẩn:

```powershell
.\scripts\Initialize-RagSecrets.ps1
```

Nếu đã có `rag-service/.env` cần import:

```powershell
.\scripts\Initialize-RagSecrets.ps1 -ImportExistingDotEnv
```

Build/rebuild và chạy container:

```powershell
.\scripts\Deploy-Rag.ps1
docker compose -f compose.rag.yml ps
```

Mỗi khi sửa code trong `rag-service`, cần chạy lại `Deploy-Rag.ps1` hoặc:

```powershell
docker compose -f compose.rag.yml up -d --build rag
```

Các named volume `rag_chroma`, `rag_knowledge` và `rag_models` giữ dữ liệu qua các lần recreate container. Không dùng `docker compose down -v` nếu muốn giữ knowledge base.

## Chia sẻ RAG qua Tailscale

Trên máy chạy container:

```powershell
.\scripts\Setup-TailscaleRag.ps1
tailscale serve status
```

Trên máy chạy Web, lưu URL và service token bằng .NET User Secrets:

```powershell
dotnet user-secrets set "RagService:BaseUrl" "https://ten-may.tailnet.ts.net" --project src\DocumentChatbot.Web
dotnet user-secrets set "RagService:ServiceToken" "<RAG_SERVICE_TOKEN>" --project src\DocumentChatbot.Web
```

OpenAI API key chỉ nằm trên máy chủ RAG. Thành viên chỉ nhận URL Tailscale và RAG service token.

## Knowledge snapshot

RAG duy trì hai lớp dữ liệu runtime:

- `chroma_db/` hoặc volume `rag_chroma`: vector index dùng cho retrieval.
- `knowledge/` hoặc volume `rag_knowledge`: snapshot JSON chuẩn hóa để kiểm tra và rebuild.

Mỗi tài liệu có snapshot riêng trong `knowledge/documents/`; `manifest.json` chỉ giữ danh sách, content hash và số section/chunk. Khi xóa tài liệu qua ứng dụng, bản ghi SQL, vector và snapshot đều được xóa.

Nếu Chroma bị mất nhưng snapshot còn nguyên:

```powershell
cd rag-service
.\.venv\Scripts\Activate.ps1
python rebuild_knowledge.py
```

## Chạy test

.NET:

```powershell
dotnet test tests\DocumentChatbot.Web.Tests\DocumentChatbot.Web.Tests.csproj
```

Python RAG:

```powershell
.\rag-service\.venv\Scripts\python.exe -m unittest discover -s rag-service\tests -v
```

Trước khi nộp final, cả hai test suite phải pass và các test Python cần được track trong Git.

## Bộ đánh giá 50 câu + ground truth

Rubric yêu cầu một test set tối thiểu 50 câu được chuẩn bị thủ công. Repository cần bổ sung artifact, ví dụ `evaluation/ground_truth_50.csv`, với các cột tối thiểu:

| Cột | Ý nghĩa |
|---|---|
| `question` | Câu hỏi kiểm thử |
| `expected_answer` | Câu trả lời chuẩn do nhóm chuẩn bị |
| `expected_document` | Tài liệu nguồn mong đợi |
| `expected_page_or_slide` | Page/slide chứa bằng chứng |
| `answer_language` | Ngôn ngữ của chunk/câu trả lời mong đợi |
| `category` | Chủ đề hoặc chương |
| `actual_answer` | Câu trả lời chatbot khi chạy đánh giá |
| `retrieved_chunks` | Các chunk thực tế được retrieval |
| `answer_correct` | Kết quả đối chiếu answer-ground truth |
| `citation_correct` | Citation có trỏ đúng nguồn hay không |

Nên báo cáo riêng ít nhất ba chỉ số: answer accuracy, citation accuracy và retrieval hit rate. Bộ dữ liệu này chưa được sinh tự động từ chatbot vì ground truth phải do con người chuẩn bị.

## Troubleshooting

- `Invalid column name 'InputTokens'`: chạy lại `database\DocumentChatbotDB.sql` để nâng cấp schema.
- RAG `/health` OK nhưng chat lỗi: kiểm tra `RagService:BaseUrl`, service token và log `/ask`.
- Score retrieval cao nhưng out-of-scope: đọc trường `text=` trong log candidate; score cao không thay thế việc chunk phải chứa đáp án.
- Sửa code Python nhưng container vẫn chạy code cũ: rebuild image bằng `docker compose ... up -d --build rag`.
- Knowledge base có tài liệu mồ côi hoặc trùng: xóa tài liệu qua UI/API rồi upload lại để SQL, Chroma và snapshot đồng bộ.

## Final submission checklist

- [x] Flow 1 — Upload & Manage Documents
- [x] Flow 2 — Questions & Answers
- [x] Flow 3 — Report & Statistics
- [x] Phân quyền Subject Leader/Student
- [x] Citation, out-of-scope và chat history
- [ ] 50 câu hỏi + human-prepared ground truth
- [ ] .NET và Python test suites pass 100%
- [ ] Commit/push toàn bộ thay đổi và kiểm tra README trên GitHub
- [ ] Upload/index tài liệu PRN222 dùng cho demo
