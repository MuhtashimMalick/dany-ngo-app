# ER Diagram — NGO Fund Management System (Phase 1)

Companion to `docs/schema.md`. Identity plumbing tables (`user_roles`, `user_claims`,
`role_claims`, `user_logins`, `user_tokens`) are omitted for readability — they are standard
ASP.NET Identity join tables with no NGO-specific columns.

```mermaid
erDiagram
    USERS ||--o{ REFRESH_TOKENS : "has"
    USERS }o--o{ ROLES : "assigned"
    ROLES }o--o{ PERMISSIONS : "granted (role_permissions)"

    FUND_CATEGORIES ||--o{ DONATIONS : "receives"
    FUND_CATEGORIES ||--o{ APPLICATIONS : "funds"
    FUND_CATEGORIES ||--o{ PAYMENTS : "funds"
    FUND_CATEGORIES ||--o{ FUND_TRANSACTIONS : "ledger for"

    DONORS ||--o{ DONATIONS : "gives"
    DONATIONS ||--o| FUND_TRANSACTIONS : "posts credit"

    APPLICATION_CATEGORIES ||--o{ APPLICATIONS : "categorizes"

    APPLICANTS ||--o{ APPLICATIONS : "files"
    APPLICANTS |o--o| DOCUMENTS : "profile photo"

    APPLICATIONS ||--o{ APPLICATION_STATUS_HISTORY : "logs"
    APPLICATIONS ||--o{ APPLICATION_REMARKS : "has"
    APPLICATIONS ||--o{ PAYMENTS : "disbursed via"

    PAYMENTS ||--o| FUND_TRANSACTIONS : "posts debit"

    APPLICANTS ||--o{ DOCUMENTS : "owns"
    APPLICATIONS ||--o{ DOCUMENTS : "owns"
    DONATIONS ||--o{ DOCUMENTS : "owns"
    PAYMENTS ||--o{ DOCUMENTS : "owns"

    USERS {
        uuid id PK
        string full_name
        string designation
        bool is_active
        bool must_change_password
    }

    FUND_CATEGORIES {
        uuid id PK
        string code UK
        string name
        bool is_zakat
    }

    APPLICATION_CATEGORIES {
        uuid id PK
        string code UK
        string name
        bool is_zakat_eligible
    }

    DONORS {
        uuid id PK
        string donor_code UK
        string full_name
        string cnic UK "nullable"
    }

    DONATIONS {
        uuid id PK
        string donation_number UK
        uuid donor_id FK
        uuid fund_category_id FK
        numeric amount
        string status
    }

    APPLICANTS {
        uuid id PK
        string cnic UK
        string full_name
        uuid photo_document_id FK "nullable"
    }

    APPLICATIONS {
        uuid id PK
        string application_number UK
        uuid applicant_id FK
        uuid application_category_id FK
        uuid fund_category_id FK
        numeric requested_amount
        numeric approved_amount "nullable"
        string status
    }

    APPLICATION_STATUS_HISTORY {
        uuid id PK
        uuid application_id FK
        string from_status "nullable"
        string to_status
    }

    APPLICATION_REMARKS {
        uuid id PK
        uuid application_id FK
        string remark
        bool is_internal
    }

    PAYMENTS {
        uuid id PK
        string payment_number UK
        uuid application_id FK
        uuid fund_category_id FK
        numeric amount
        string status
    }

    FUND_TRANSACTIONS {
        uuid id PK
        uuid fund_category_id FK
        string direction "Credit/Debit"
        numeric amount
        string reference_type
        uuid reference_id "unique with reference_type"
    }

    DOCUMENTS {
        uuid id PK
        string storage_key UK
        string document_type
        uuid applicant_id FK "nullable, exactly one owner set"
        uuid application_id FK "nullable"
        uuid donation_id FK "nullable"
        uuid payment_id FK "nullable"
    }

    ROLES {
        uuid id PK
        string name UK
        bool is_system
    }

    PERMISSIONS {
        uuid id PK
        string code UK
        string module
    }

    REFRESH_TOKENS {
        uuid id PK
        uuid user_id FK
        string token_hash UK
        timestamptz expires_at
    }
```
