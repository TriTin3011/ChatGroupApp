# ChatGroupApp

Lab 01 chat group app gom:

- `ChatGroupApp.Server`: Console app TCP server.
- `ChatGroupApp.Client`: WPF app TCP client co emoji picker.

## Cach chay

1. Mo terminal tai thu muc solution.
2. Chay server:

```powershell
dotnet run --project ChatGroupApp.Server
```

Server se hoi port. Bam Enter de dung port mac dinh `5000`, hoac nhap port khac.
Man hinh server se hien:

- `Server running at port: ...`
- Danh sach `IP server` de client ket noi.

3. Chay client:

```powershell
dotnet run --project ChatGroupApp.Client
```

4. Tren client:

- Nhap `IP Server` theo IP server hien trong Console. Neu chay cung may, co the dung `127.0.0.1`.
- Nhap `Port`, mac dinh la `5000`.
- Nhap ten cua ban.
- Bam `Ket noi`.
- Bam nut emoji de chen emoji vao tin nhan.
- Bam `Gui` hoac Enter de gui tin nhan.

Co the mo nhieu cua so client de test chat group.
