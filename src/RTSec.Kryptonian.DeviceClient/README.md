# Kryptonian Device Client

Small console application that behaves like a medical device requesting approval from a MEDIATE gateway.

The client posts device identity metadata to:

```http
POST /api/device-requests
```

The gateway creates a pending device record. An administrator must approve that device before EST enrollment is allowed.

## Run

```powershell
dotnet run --project src/RTSec.Kryptonian.DeviceClient -- --gateway http://localhost:5000 --name "Scanner 7" --cn scanner-7 --serial SCAN-7
```

Options:

- `--gateway`: Gateway base URL. Defaults to `KRYPTONIAN_GATEWAY_URL` or `http://localhost:5000`.
- `--name`: Device display name.
- `--cn`: Subject common name the device will later use in its EST CSR.
- `--manufacturer`: Device manufacturer.
- `--model`: Device model.
- `--serial`: Device serial number.
