# Kryptonian Device Client

Small console application that behaves like a medical device activating through the Kryptonian gateway.

The administrator first registers the device alias in the Devices tab and gives the generated activation code to the device. The client then sends a CSR to the device-facing EST endpoint over HTTPS:

```http
POST /.well-known/est/simpleenroll
```

The activation code is sent as the one-time shared secret for the first certificate. After the first certificate is issued, the gateway consumes the activation code and marks the device active.

## Run

```powershell
dotnet run --project src/RTSec.Kryptonian.DeviceClient -- --gateway https://localhost:8443 --name "Scanner 7" --cn scanner-7 --serial SCAN-7 --activation-code ABCD-EFGH
```

Options:

- `--gateway`: Gateway EST base URL. Defaults to `KRYPTONIAN_GATEWAY_URL` or `https://localhost:8443`.
- `--name`: Device display name.
- `--cn`: Subject common name in the EST CSR.
- `--manufacturer`: Device manufacturer.
- `--model`: Device model.
- `--serial`: Device serial number.
- `--activation-code`: One-time activation code generated from the Devices tab. You can also set `KRYPTONIAN_ACTIVATION_CODE`.
