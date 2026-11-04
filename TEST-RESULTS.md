\# AcxiomCRM — Test Results



Tested on: 04-Nov-2026 · Environment: local (LocalDB, .NET 10)

Test users: admin@acxiom.com (Admin), manager@acxiom.com (Manager), sales@acxiom.com (SalesExecutive)



\## 17.19 Final Acceptance Scenario

| # | Scenario | Result |

|---|----------|--------|

| 1 | Protected pages blocked without login | ✅ Pass |

| 2 | Register/login reaches Dashboard | ✅ Pass |

| 3 | Invalid email/phone blocked client-side | ✅ Pass |

| 4 | Crafted request rejected server-side | ✅ Pass |

| 5 | Opportunity Amount <= 0 rejected | ✅ Pass |

| 6 | Probability > 100 rejected | ✅ Pass |

| 7 | Past Expected Close Date rejected (active opportunity) | ✅ Pass |

| 8 | Follow-up dated before today rejected | ✅ Pass |

| 9 | SalesExecutive limited to assigned records (URL tampering → Access Denied) | ✅ Pass |

| 10 | Manager sees team pipeline/reports, no admin | ✅ Pass |

| 11 | Admin user/role/audit administration available | ✅ Pass |

| 12 | Create/update/delete generates audit entries | ✅ Pass |

| 13 | GET /api/customers returns authorized JSON | ✅ Pass |

| 14 | Dashboard KPI cards + Chart.js charts | ✅ Pass |



\## Security checks

| Check | Result |

|-------|--------|

| Passwords stored only as Identity hashes | ✅ Pass |

| Password policy enforced | ✅ Pass |

| Account lockout after 5 failed attempts | ✅ Pass |

| Deactivated users cannot log in | ✅ Pass |

| Anti-forgery token on all state-changing forms | ✅ Pass |

| Audit log append-only (DB trigger blocks UPDATE/DELETE) | ✅ Pass |



| API: 401 without token, 403 wrong role, 429 rate limit | ✅ Pass |

| No passwords/hashes/tokens in API responses or logs | ✅ Pass |



\## API status codes verified (Postman collection: docs/AcxiomCRM.postman\_collection.json)

200, 201 (+Location header), 400, 401, 403, 404, 409, 429

