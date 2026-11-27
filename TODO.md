# TODO

## Deployment documentation

- [ ] Document deployment of downloadable reports. The release includes a demonstration `reports.html`; replace it with the deployment-specific page linking to actual report files. For a deployment using a `bin` directory, place Excel files under `bin/wwwroot/ods/Excel/` and use root-relative links such as `/ods/Excel/Report1.xlsx`. Explain that `wwwroot` is the application's web root and that paths must match the deployed files. Verify downloads in each environment. Documentation only; no application code changes required.
