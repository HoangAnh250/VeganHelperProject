# Hosting Vegan Helper System trên Azure và sử dụng CI/CD

Hướng dẫn dành cho hai repo hiện tại và tài khoản Azure for Students. Pipeline đã được viết ở local; chưa có nghĩa file đã lên GitHub, tài nguyên Azure đã được tạo hoặc website đã deploy. Không chia sẻ mật khẩu/API key trong chat hay commit vào Git.

## 1. Kiến trúc và các nhánh

| Thành phần | Dịch vụ | Nhánh CI | Nhánh deploy |
|---|---|---|---|
| BE .NET 10 | Azure App Service Linux | `developer`, `main`, PR vào hai nhánh | `main` |
| FE React/Vite | Azure Static Web Apps | `dev`, `main`, PR vào hai nhánh | `main` |
| Database | Supabase PostgreSQL hiện tại | CI dùng PostgreSQL local riêng | Không tự migrate/seed |
| Ảnh/avatar | Cloudflare R2 hiện tại | Test không gọi storage thật | Giữ R2 |

File BE: `.github/workflows/backend.yml`. File FE: `.github/workflows/frontend.yml` và `public/staticwebapp.config.json`.

BE restore → build Release → unit/integration test → publish → upload artifact. FE `npm ci` → test → build → kiểm tra SPA config → upload artifact. Job deploy chỉ chạy sau CI thành công, trên `main`, khi repository variable `AZURE_DEPLOY_ENABLED` bằng chuỗi `true`. Push vào `developer`/`dev` và PR không deploy.

Manual run mặc định chỉ CI. Muốn deploy: chọn branch `main`, bật input `deploy` và đã đặt `AZURE_DEPLOY_ENABLED=true`. Thao tác này chạy lại kiểm thử/build rồi deploy đúng artifact của lần chạy đó. [GitHub Actions cho App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions)

**Reset password đã dùng URL cấu hình:** BE lấy `Frontend:BaseUrl` để tạo link `/forgot-password?token=...&email=...`. Local Development/Testing mặc định `http://localhost:3000`; Production phải cấu hình URL HTTPS của FE, không dùng localhost/loopback. URL chỉ là origin, không có path, credentials, query hoặc fragment; dấu `/` cuối được chuẩn hóa. BE kiểm tra cấu hình ngay khi startup để tránh gửi link sai. Token vẫn được lưu dạng hash, hết hạn sau 15 phút và chỉ dùng một lần; response không tiết lộ email có tồn tại hay không. CORS và `VITE_API_BASE_URL` không thay thế cấu hình URL trong email.

## 2. Chuẩn bị trước khi tạo tài nguyên

1. Đăng nhập [Azure Portal](https://portal.azure.com), vào **Subscriptions** và xác nhận subscription Azure for Students đang Active.
2. Kiểm tra credit còn lại trong Education Hub/Sponsorships và giá gói được chọn. Chương trình Student có credit nhưng tài nguyên trả phí vẫn sử dụng credit; không xem B1 là hosting miễn phí không giới hạn. [Azure for Students](https://azure.microsoft.com/en-us/free/students/)
3. Bạn cần quyền tạo resource/managed identity và gán role trên subscription/resource cần dùng. Nếu tài khoản trường giới hạn IAM, nhờ quản trị viên Azure hỗ trợ đúng quyền bị thiếu.
4. Hai repository do bạn của bạn sở hữu. Người có quyền phù hợp phải thêm GitHub secrets/variables và cho phép Actions chạy. Không cần chuyển quyền sở hữu repo hay trao mật khẩu GitHub/Azure.
5. Thống nhất phát hành từ `main`. Các commit merge local trước đó và file pipeline mới vẫn cần được review, đưa vào `main` và push khi được phép. Azure không đọc file chỉ có trên máy bạn.
6. Không cấu hình thêm workflow auto-generated song song với hai workflow này; nếu đã có, review/tắt workflow trùng để tránh deploy hai lần.

## 3. Tạo resource group và hosting BE

### 3.1 Resource group

Trong Azure Portal → **Resource groups → Create**:

- Subscription: Azure for Students của bạn.
- Resource group: ví dụ `rg-vegan-helper`.
- Region: chọn vùng được subscription cho phép, ưu tiên gần người dùng và Supabase; Singapore/Southeast Asia là một lựa chọn nếu có quota.
- **Review + create → Create**.

Tên trong hướng dẫn chỉ là ví dụ; không có tài nguyên nào tự được tạo khi đọc tài liệu.

### 3.2 Web App

Azure Portal → **App Services → Create → Web App**:

- Subscription/resource group: chọn đúng mục vừa tạo.
- Name: đặt tên Web App khả dụng, ví dụ `vegan-helper-api-<ten-ban>`.
- Publish: **Code**.
- Runtime stack: **.NET 10**; project hiện là `net10.0`.
- Operating System: **Linux**.
- Region: chọn vùng hợp lệ.
- Linux App Service Plan: tạo một plan dành cho BE.
- Pricing plan: đề xuất **Basic B1** để có Always On; xem giá trước khi tạo. Nếu quota/SKU không khả dụng, chọn vùng/gói hợp lệ với ngân sách. Không chọn .NET 8 để chạy DLL net10.0.
- Deployment/continuous deployment: để tắt trong lúc tạo, vì repo đã có workflow riêng.
- Review và tạo.

Trong **Overview**, ghi lại:

| Giá trị | Cách lấy |
|---|---|
| Web App Name | Tên resource, không phải URL |
| BE Base URL | Copy URL HTTPS/default domain được Azure cấp, không thêm `/api` |
| Subscription ID | Subscription đang chứa Web App |
| Directory/Tenant ID | Directory chứa subscription/identity |

Không đoán URL từ tên app: hostname mới có thể có hậu tố/vùng. Trong Settings/Configuration (tên menu có thể thay đổi): startup command `dotnet VeganHelper.API.dll`, bật HTTPS Only và **Always On**. Always On giúp app không unload vì không có truy cập; không bảo đảm worker không bao giờ restart. Free/Shared không phù hợp nếu cần worker chạy liên tục. [Cấu hình App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-common)

App Service Plan vẫn có thể phát sinh phí khi chỉ Stop Web App. Kiểm tra Cost Management; không xóa resource group nếu còn tài nguyên cần giữ. [Chi phí App Service](https://learn.microsoft.com/en-us/azure/app-service/overview-manage-costs)

### 3.3 Cấu hình BE trong Azure

Vào Web App → **Settings → Environment variables → App settings → Add**. Đây là cấu hình runtime của BE, khác với GitHub secrets dùng để deploy. Tên lồng nhau của .NET dùng hai dấu gạch dưới `__`. Apply sẽ restart app. [Biến môi trường App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-common)

| Tên | Giá trị/cách dùng |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` cho App Service Linux để nhận scheme HTTPS từ proxy Azure; chỉ dùng trong môi trường proxy được kiểm soát |
| `ConnectionStrings__DefaultConnection` | Chuỗi Npgsql của Supabase; không dùng SQL Server |
| `ConnectionStrings__HangfireConnection` | Tùy chọn; không tạo biến này thì dùng DefaultConnection. Nếu tạo, phải là connection string hợp lệ, không để giá trị rỗng |
| `Jwt__SigningKey` | Secret đủ mạnh, ít nhất 32 ký tự; giữ ổn định qua các deploy |
| `Jwt__Issuer` | `VeganHelper.API` hoặc giá trị nhất quán nhóm chọn |
| `Jwt__Audience` | `VeganHelper.Client` hoặc giá trị nhất quán nhóm chọn |
| `Jwt__AccessTokenMinutes` | Ví dụ `30` |
| `Jwt__RefreshTokenDays` | Ví dụ `7` |
| `Google__ClientId` | Cùng Google Client ID mà FE dùng |
| `Frontend__BaseUrl` | URL HTTPS của FE, ví dụ `https://<site>.azurestaticapps.net`; không thêm `/forgot-password` hoặc `/api` |
| `SendGrid__ApiKey` | API key SendGrid |
| `SendGrid__FromEmail` | Email người gửi đã verify với SendGrid |
| `SendGrid__FromName` | Ví dụ `VeganHelper` |
| `CloudflareR2__BucketName` | Bucket hiện tại |
| `CloudflareR2__AccessKey` | R2 access key |
| `CloudflareR2__SecretKey` | R2 secret key |
| `CloudflareR2__ServiceUrl` | Endpoint S3 của R2 hiện tại |
| `CloudflareR2__PublicUrl` | URL public của media hiện tại |
| `Cors__AllowedOrigins__0` | URL HTTPS FE, không thêm dấu `/` cuối hoặc path |
| `DeepSeekChat__ApiKey` | DeepSeek key for AI Genie chatbot; keep on Backend only |
| `DeepSeekChat__Model` | `deepseek-flash` |
| `ModerationAi__Enabled` | Khởi đầu `false`; bật khi đã xác nhận schema Hangfire và cấu hình AI |
| `ModerationAi__ApiKey` | Gemini key dùng cho moderation |
| `ModerationAi__Model` | `gemini-3.5-flash-lite` (cần quota trong project Google đang dùng) |
| `ModerationAi__TimeoutSeconds` | Ví dụ `60` |
| `ModerationAi__MinAutoPublishConfidence` | `0.9` theo cấu hình hiện tại |
| `ModerationAi__AllowedMediaHosts__0` | **Hostname** media R2 public, không phải full URL; thêm __1, __2 nếu cần |
| `WebPush__Enabled` | Khởi đầu `false`, bật khi đã chuẩn bị Hangfire/VAPID |
| `WebPush__Subject` | Giá trị VAPID hợp lệ, ví dụ email liên hệ `mailto:...` |
| `WebPush__PublicKey`, `WebPush__PrivateKey` | Cặp VAPID của server; private key chỉ ở BE |
| `AutoMapper__LicenseKey` | Theo cấu hình/license nhóm sử dụng, nếu cần |

Trong cấu hình Linux, forwarded headers giúp tránh redirect loop khi Azure terminate TLS trước app. Không áp dụng giá trị này cho server bất kỳ tiếp nhận header giả mạo trực tiếp từ Internet. [ASP.NET Core và reverse proxy](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)

Trong Supabase → **Connect**, chọn direct connection nếu mạng hỗ trợ, hoặc **Session pooler**. Chuỗi mẫu, không nhập nguyên placeholder:

```text
Host=<session-pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<database-password>;SSL Mode=VerifyFull
```

Không mang đường dẫn certificate tuyệt đối từ máy D: lên Azure. Publish đã giữ certificate Supabase có sẵn trong dự án. Giữ xác minh TLS; dùng đúng host/connection của Supabase. [Kết nối PostgreSQL](https://supabase.com/docs/guides/database/connecting-to-postgres)

Trước khi deploy phiên bản BE này, phải đặt `Frontend__BaseUrl` bằng origin FE lấy từ Static Web App Overview. Nếu chưa tạo FE resource, tạo resource để lấy URL trước; chưa cần deploy giao diện. Khi Production còn dùng mặc định localhost hoặc URL không hợp lệ, BE sẽ không khởi động và health check của pipeline sẽ thất bại.

User-secrets, `.env`, `supabase.local.json` và appsettings private trên máy không được pipeline đưa lên Azure. Gói BE publish không chứa các appsettings local; phải nhập cấu hình runtime trên Azure/Key Vault. Thiếu JWT/DB có thể khiến app không start; thiếu R2/email có thể làm API liên quan lỗi dù `/health/live` vẫn trả 200.

### 3.4 Hangfire và database hiện có

Pipeline **không chạy** `--migrate`, `--seed`, `--seed-posts` hoặc `--prepare-background-jobs` trên Supabase. Không xóa/import lại dataset.

Với Supabase đang dùng: kiểm tra migration cần thiết đã áp dụng và schema Hangfire đã được chuẩn bị trong các lần trước. Nếu đổi sang database mới, phải review và áp dụng migration/chuẩn bị Hangfire như một thao tác riêng, có quyền và chấp thuận; không nhét vào startup hoặc pipeline deploy.

Sau khi đã xác nhận schema và credentials, bật `ModerationAi__Enabled`/`WebPush__Enabled` theo chức năng muốn chạy. App restart sau Apply. Nếu recurring-job lock timeout, xem log và cấu hình pooler/các instance đang chạy; không xóa lock hoặc schema chung tùy tiện.

Follower notification worker hiện chạy khi môi trường không phải Testing. Cả BE local và Azure có thể xử lý cùng queue khi cùng database. Không dùng môi trường `Testing` cho production để né worker; khi cần tách môi trường, dùng database phát triển riêng hoặc thiết kế công tắc worker riêng trong tác vụ tiếp theo.

## 4. Cấu hình OIDC để GitHub deploy BE

BE dùng OIDC, không cần publish profile hay Azure client secret. Identity deploy khác với JWT secret, Gemini key và mật khẩu Supabase. Chỉ job deploy được quyền `id-token: write`. [OIDC trên Azure](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-azure)

### 4.1 Tạo managed identity

Azure Portal → tìm **Managed Identities → Create**:

1. Chọn subscription Student và resource group chứa Web App.
2. Đặt tên, ví dụ `id-vegan-helper-github-deploy`.
3. Chọn region hợp lệ và tạo **user-assigned managed identity**.
4. Mở Overview và copy **Client ID**. Không nhầm Client ID với Object/Principal ID.

Không bắt buộc gắn identity deploy này vào BE đang chạy chỉ để GitHub đăng nhập OIDC.

### 4.2 Thêm federated credential

Trong managed identity → **Settings → Federated credentials → Add credential**:

- Scenario: **GitHub Actions deploying Azure resources** (hoặc lựa chọn tương đương).
- Organization/owner: **chủ repo BE**, hiện tại `HoangAnh250`; không phải username collaborator của bạn.
- Repository: `VeganHelperProject`.
- Entity type: **Branch**.
- Branch: **main** (đúng chữ hoa/thường).
- Name: ví dụ `github-be-main`.

Với cấu hình advanced, các trường tương ứng là:

```text
Issuer: https://token.actions.githubusercontent.com
Subject: repo:HoangAnh250/VeganHelperProject:ref:refs/heads/main
Audience: api://AzureADTokenExchange
```

Nếu repo được đổi owner/tên, sửa subject tương ứng. Workflow hiện không đặt GitHub environment; nếu sau này thêm `environment: production`, subject OIDC phải đổi sang loại environment. Không dùng subject branch cũ cho job có environment.

### 4.3 Gán quyền chỉ trên Web App

Mở **Web App → Access control (IAM) → Add role assignment**:

1. Role: **Website Contributor**.
2. Assign access to: **Managed identity**.
3. Chọn user-assigned identity vừa tạo.
4. Review + assign.

Làm tại Web App để scope giới hạn app này; không cấp Owner toàn subscription cho pipeline. Nếu không có quyền gán role, cần người quản trị có quyền IAM hỗ trợ. Có thể cần đợi vài phút để quyền/federation có hiệu lực. [App Service GitHub Actions](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions)

### 4.4 Thêm BE GitHub secrets và variables

Repo **BE** → **Settings → Secrets and variables → Actions**. Nếu không có quyền Settings, nhờ chủ repo làm bước này; không gửi các secret qua chat.

Tab **Secrets → New repository secret**:

| Tên | Giá trị |
|---|---|
| `AZURE_CLIENT_ID` | Client ID của managed identity deploy |
| `AZURE_TENANT_ID` | Tenant/Directory ID chứa identity |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID chứa Web App |

Tab **Variables → New repository variable**:

| Tên | Giá trị |
|---|---|
| `AZURE_WEBAPP_NAME` | Tên resource Web App |
| `AZURE_WEBAPP_URL` | Base URL HTTPS copy từ Overview; không có `/api` |
| `AZURE_DEPLOY_ENABLED` | Để `false` trong lúc chuẩn bị; đặt `true` sau khi runtime/OIDC đã xong |

BE không cần Supabase/Gemini/R2 credentials trong GitHub Actions. CI chỉ dùng PostgreSQL service riêng; runtime secrets đặt tại App Service.

## 5. Tạo hosting FE và cấu hình pipeline

### 5.1 Tạo Static Web App

Azure Portal → **Static Web Apps → Create**:

- Subscription: Azure for Students.
- Resource group: ví dụ `rg-vegan-helper`.
- Name: ví dụ `vegan-helper-web`.
- Hosting plan: **Free** cho demo nếu đáp ứng nhu cầu/quota.
- Region: vùng hợp lệ.
- Deployment source: chọn **Other** nếu có, để dùng workflow đã chuẩn bị và không tự tạo workflow trùng.
- Review + create.

Nếu chọn GitHub thay vì Other, chọn đúng repo FE `NguyenQuocTrong-bonmat/frontend-VeganHelperSystem`, branch `main`, app location `/`, API location trống, output `build`. Azure có thể tạo thêm workflow/token tự động: review và giữ **một** đường deploy; pipeline trong tài liệu dùng token có tên cố định bên dưới, nên tên secret ngẫu nhiên Azure tạo không tự khớp.

Mở Overview, copy **FE URL HTTPS** Azure cấp. Không cần mua domain để demo. Static Web Apps có gói Free và certificate TLS tự quản lý. [Các gói hosting](https://learn.microsoft.com/en-us/azure/static-web-apps/plans)

### 5.2 Deployment token và secrets FE

Static Web App → **Overview → Manage deployment token** → copy token; không chọn Reset nếu chỉ cần lấy token hiện tại. Token có quyền deploy site, nên chỉ nhập trực tiếp vào GitHub secret của đúng repo. [Deployment token](https://learn.microsoft.com/en-us/azure/static-web-apps/deployment-token-management)

Repo **FE** → **Settings → Secrets and variables → Actions → Secrets**:

| Tên secret | Giá trị |
|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Deployment token của Static Web App vừa tạo |
| `VITE_API_BASE_URL` | `https://<BE-hostname>/api` |
| `VITE_GOOGLE_CLIENT_ID` | Client ID cùng với `Google__ClientId` BE |
| `VITE_GOONG_MAPTILES_KEY` | Goong Maptiles key hiện tại |

Tab Variables: thêm `AZURE_DEPLOY_ENABLED=false` lúc chuẩn bị; đổi `true` khi sẵn sàng. Đây là variable **riêng trong FE repo**, không tự lấy từ BE repo.

FE không dùng database connection string, JWT signing key, SendGrid hay Gemini secret. Các `VITE_*` dù lưu trong GitHub Secrets vẫn được đưa vào bundle mà trình duyệt đọc được; không xem chúng là backend secrets.

Khi deploy bật, pipeline chặn API URL trống, localhost, HTTP, URL có credentials/query và URL không kết thúc bằng `/api`. Google/Goong có thể bỏ trống để CI build, nhưng chức năng liên quan sẽ không hoạt động đầy đủ; cấu hình chúng trước khi demo các chức năng đó.

### 5.3 Nối FE và BE

1. App Service BE: đặt `Cors__AllowedOrigins__0` bằng URL HTTPS FE, không có path hoặc dấu `/` cuối. Giữ một cấu hình CORS ở code BE; không bật thêm danh sách CORS khác trong Portal mà chưa đối chiếu.
2. Google Cloud OAuth Client: thêm FE origin HTTPS vào **Authorized JavaScript origins**; giữ `http://localhost:3000` nếu vẫn phát triển local. Với flow GoogleLogin hiện tại dùng credential callback trong JS, không tự invent redirect URI; chỉ thêm redirect URI khi flow thực tế yêu cầu.
3. Goong dashboard: cập nhật hạn chế domain/referrer nếu key đang giới hạn localhost.
4. Kiểm tra ảnh/video R2 public qua HTTPS và hostname ảnh có trong allowlist moderation.
5. Đặt `Frontend__BaseUrl` bằng origin FE ở BE để link reset password dẫn đúng website; giá trị này độc lập với CORS và các `VITE_*`.
6. Apply cấu hình BE; thay đổi `Frontend__BaseUrl` cần restart BE (Azure Apply thực hiện restart), không cần build FE. Thay đổi `VITE_*` cần build/deploy FE lại.

Không chạy `npm start`/Vite dev server trên hosting FE. Pipeline build ra HTML/CSS/JS, Static Web Apps phục vụ các file đó. `public/staticwebapp.config.json` được copy vào build để URL React Router mở trực tiếp và trang 404 hoạt động. Với prebuilt upload, app_location=`build`, output_location trống, skip_app_build=true. [Cấu hình prebuilt](https://learn.microsoft.com/en-us/azure/static-web-apps/build-configuration#skip-building-front-end-app)

## 6. Đưa pipeline lên GitHub và deploy lần đầu

1. Review các file workflow/guide và thay đổi csproj BE. Không stage appsettings private hoặc `.env`.
2. Commit trên nhánh riêng rồi tích hợp vào `developer`/`dev`, sau đó review/merge vào `main` của từng repo theo quy định nhóm. Nếu nhờ trợ lý commit/push, cần yêu cầu rõ; lượt tạo pipeline này chưa tự commit/push.
3. Sau khi push `main`, GitHub → **Actions** sẽ thấy **Backend CI/CD** hoặc **Frontend CI/CD**. Nếu workflow chưa có trên default branch, nút manual run có thể chưa xuất hiện.
4. Để `AZURE_DEPLOY_ENABLED=false` ban đầu: CI chạy, job deploy hiển thị skipped là đúng. Kiểm tra BE build/test/publish và FE build/test đều xanh.
5. Nhập đầy đủ runtime settings, OIDC, token và `VITE_*`; bật BE variable `AZURE_DEPLOY_ENABLED=true`.
6. Actions BE → **Backend CI/CD → Run workflow** → branch **main** → bật **deploy** → Run. Xem log job deploy; sau deploy pipeline gọi `/health/live`.
7. Mở BE URL `/health/live`, mong đợi HTTP 200 và `status: ok`. Thử thêm API đọc dữ liệu để xác nhận Supabase; liveness không kiểm tra DB/email/AI.
8. Bật FE variable `AZURE_DEPLOY_ENABLED=true`; Actions FE → **Frontend CI/CD → Run workflow** → branch **main** → bật **deploy** → Run. Job deploy upload artifact đã test và kiểm tra FE URL trả thành công.
9. Mở FE URL, kiểm tra các luồng thật bên dưới.

Manual run không bật `deploy` luôn chỉ kiểm tra, kể cả `AZURE_DEPLOY_ENABLED=true`. Khi cả hai repo đã bật auto-deploy, những lần push/merge tiếp theo vào `main` sẽ tự deploy sau CI thành công.

Trong **Settings → Branches/Rulesets** nếu nhóm có quyền: yêu cầu check `Backend build and tests` cho BE, `Frontend build and tests` cho FE và review trước khi merge. Khả năng cấu hình phụ thuộc loại repo/gói GitHub. PR chưa merge không đưa code lên production.

## 7. Kiểm tra sau deploy

| Kiểm tra | Kết quả cần thấy |
|---|---|
| Home/feed | Gọi BE HTTPS đúng host, hiển thị dữ liệu |
| Login thường/Remember me | Token và My Posts đúng user |
| Google login | Không báo origin không được phép |
| Profile, ảnh/avatar | Đọc/upload R2 thành công |
| Create/Edit post | Dữ liệu/media đúng; kiểm duyệt đúng trạng thái |
| FN26 | Tạo meal plan từ hồ sơ và dữ liệu chung |
| Follow, like, comment | Thông báo đúng người, không tự thông báo cho chính mình |
| AI/push worker | Chỉ kiểm tra khi đã bật và đủ config/schema/quota |
| URL trực tiếp `/profile`, `/posts/...` | Không bị 404 của hosting; auth/resource xử lý đúng |
| URL không tồn tại | Hiển thị trang 404 React |
| Reset password email | Email chứa URL FE HTTPS đúng `/forgot-password`; reset thành công với token mới, token đã dùng/hết hạn bị từ chối |

Swagger hiện chỉ có ở Development, nên `/swagger` không hoạt động khi production là hành vi có chủ đích; không đổi production sang Development để mở Swagger.

## 8. Lỗi thường gặp và nơi kiểm tra

| Triệu chứng | Kiểm tra/cách xử lý |
|---|---|
| CI xanh nhưng deploy skipped | Variable `AZURE_DEPLOY_ENABLED`, branch và input deploy; hai repo có variable riêng |
| `VITE_API_BASE_URL` bị chặn | Secret ở FE repo, URL HTTPS đúng BE và đuôi `/api`; rerun workflow để rebuild |
| OIDC `No matching federated identity` | Subject owner/repo/branch đúng main, issuer/audience, Client ID và Tenant ID đúng |
| Azure deploy authorization failed | Website Contributor đúng Web App, identity đúng, subscription đúng, quyền đã có hiệu lực |
| Static Web Apps invalid token | Token đúng resource; secret phải tên chính xác `AZURE_STATIC_WEB_APPS_API_TOKEN` |
| `.NET SDK`/runtime mismatch | CI theo global.json; App Service phải có runtime .NET 10 và startup command đúng DLL |
| BE 500/503 hoặc startup failed | App Service **Log stream**, deployment log, runtime settings JWT/DB, prompt/certificate có trong artifact |
| HTTPS redirect loop | Forwarded headers cho Linux và HTTPS Only; không dùng localhost certificate trên Azure |
| CORS/mixed content | CORS exact FE origin, API HTTPS; kiểm tra Network của trình duyệt |
| Refresh URL bị hosting 404 | Artifact có staticwebapp.config.json; pipeline dùng build thay vì dist |
| Ảnh/upload lỗi | R2 endpoint/keys/bucket/PublicUrl và HTTPS |
| Worker ngừng khi ít truy cập | Gói có Always On, Always On bật, process/log khỏe |
| Hangfire lock timeout | Schema đã chuẩn bị, session pooler/direct và các process đang dùng database chung; không xóa lock tùy tiện |
| Không nhận email | SendGrid key/sender verified và log BE; email gửi thất bại không làm response tiết lộ tài khoản |
| BE không start sau cập nhật reset password | Kiểm tra lỗi `Frontend:BaseUrl`; đặt `Frontend__BaseUrl` là origin FE HTTPS không có path/query/fragment, không dùng localhost trong Production |
| Link reset dẫn sai website | Kiểm tra `Frontend__BaseUrl` tại App Service, Apply/restart và yêu cầu email mới. Link trong email cũ không tự đổi |
| Không thấy repo hoặc không thêm được secret | Chủ repo/organization hỗ trợ quyền GitHub; quyền Azure không tự cấp quyền GitHub |
| Azure báo quota 0/SKU không hỗ trợ | Subscription Student/quota/region; xem giá và chọn vùng/gói hợp lệ hoặc yêu cầu hỗ trợ |

Từ GitHub Actions có thể tải `backend-test-results`, `backend-publish` hoặc `frontend-build` của chính lần chạy đó. Artifacts lưu 7 ngày. CI PostgreSQL/database test được tạo lại trên runner và không dùng credential Supabase thật.

## 9. Cập nhật và khôi phục

Hằng ngày sửa trên feature → `developer`/`dev` → test/review → `main`. FE và BE độc lập; khi thêm API tương thích, deploy BE trước rồi FE. Migration là thao tác riêng: không xóa bảng/cột khi phiên bản FE/BE cũ còn cần.

Để tạm ngừng auto-deploy, đặt `AZURE_DEPLOY_ENABLED=false` trong từng repo; CI vẫn chạy. Để khôi phục bản cũ, revert qua PR hoặc redeploy artifact/commit đã kiểm thử. Rollback code không rollback database. Không force-push main và không tùy tiện chạy migration Down.

Actions được ghim commit chính thức đã xác minh để tránh tag dịch chuyển. Khi cập nhật action, kiểm tra repo/tag chính thức, input và quyền rồi cập nhật SHA qua review. Static Web Apps action dùng container do nhà cung cấp quản lý; pin action không cố định toàn bộ dịch vụ Azure.

## 10. Checklist trước khi coi là đã hosting xong

- Pipeline đã có trên main GitHub, không chỉ local.
- GitHub CI trên main đạt; deploy job không skipped và thành công.
- App Service/Static Web App đúng subscription/resource, không có workflow trùng.
- FE URL và BE URL truy cập HTTPS; CORS/Google/Goong đã cập nhật.
- Supabase, R2, SendGrid và worker được kiểm tra theo các chức năng đã bật.
- Không có secret local trong Git hoặc artifact BE.
- `Frontend__BaseUrl` đúng FE production; kiểm tra email reset thật, đường dẫn trực tiếp `/forgot-password`, token hết hạn và token không tái sử dụng.
- Đã ghi lại commit FE/BE đang phát hành và theo dõi credit/chi phí thực tế.
