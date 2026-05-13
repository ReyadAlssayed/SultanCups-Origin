window.downloadFile = (fileName, base64Data) => {

    const link = document.createElement("a");

    link.href =
        "data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64," +
        base64Data;

    link.download = fileName;

    document.body.appendChild(link);

    link.click();

    document.body.removeChild(link);
};


// طباعة pdf
window.printPurchaseCard = function () {

    const content = document.querySelector('.sal-dialog-box').outerHTML;

    const css = Array.from(document.querySelectorAll('link[rel="stylesheet"],style'))
        .map(x => x.outerHTML)
        .join('');

    const win = window.open('', '', 'width=900,height=700');

    win.document.title = "";

    win.document.write(`
        <html dir="rtl">
        <head>

            ${css}

            <title></title>

            <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800&display=swap"
                  rel="stylesheet">

            <style>

                @page{
                    size:auto;
                    margin:10mm;
                }

                body{
                    margin:0;
                    padding:0;
                    background:#ffffff;
                    font-family:'Cairo', sans-serif;
                }

                .print-head{
                    width:700px;
                    margin:0 auto 18px auto;
                    text-align:right;
                    font-size:16px;
                    font-weight:900;
                    color:#111827;
                }

                .sal-dialog-box{
                    margin:0 auto !important;
                    width:700px !important;
                    box-shadow:none !important;
                    padding-bottom:70px;
                }

                .pro-dialog-actions{
                    display:none !important;
                }

                .pro-dialog-overlay{
                    background:none !important;
                }

                .print-footer{
                    position:fixed;
                    bottom:0;
                    left:0;
                    right:0;
                    text-align:center;
                    font-size:13px;
                    font-weight:600;
                    border-top:1px solid #000;
                    padding-top:8px;
                    padding-bottom:4px;
                    background:#fff;
                }

            </style>

        </head>

        <body>

            <div class="print-head">
                مصنع السلطان للأكواب الورقية
            </div>

            ${content}

            <div class="print-footer">
                صناعة نفخر بها
            </div>

        </body>
        </html>
    `);

    win.document.close();

    const waitForLoad = setInterval(() => {

        const skeletons =
            win.document.querySelectorAll(
                '.skeleton-box,.loading-skeleton,.skeleton-card,.skeleton');

        if (skeletons.length === 0) {

            clearInterval(waitForLoad);

            win.document.fonts.ready.then(() => {

                setTimeout(() => {

                    win.print();
                    win.close();

                }, 700);

            });
        }

    }, 300);
};


window.printOrderCard = function () {

    const content = document.querySelector('.sal-dialog-box').outerHTML;

    const css = Array.from(document.querySelectorAll('link[rel="stylesheet"],style'))
        .map(x => x.outerHTML)
        .join('');

    const win = window.open('', '', 'width=900,height=700');

    win.document.title = "";

    win.document.write(`

        <html dir="rtl">

        <head>

            ${css}

            <title></title>

            <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800&display=swap"
                  rel="stylesheet">

            <style>

                @page{
                    size:auto;
                    margin:10mm;
                }

                body{
                    background:#fff;
                    font-family:'Cairo', sans-serif;
                    margin:0;
                    padding:0;
                }

                .sal-dialog-box{
                    width:700px !important;
                    margin:auto !important;
                    box-shadow:none !important;
                    padding-bottom:70px;
                }

                .btn-cancel,
                .btn-pdf-save{
                    display:none !important;
                }

                .pro-dialog-actions{
                    display:none !important;
                }

                table{
                    width:100%;
                    border-collapse:collapse;
                    margin-top:15px;
                }

                th, td{
                    border:1px solid #ddd;
                    padding:8px;
                    text-align:center;
                }

                th{
                    background:#f3f4f6;
                    font-weight:bold;
                }

                .print-only{
                    display:block !important;
                }

                img{
                    object-fit:contain;
                }

                .print-footer{
                    position:fixed;
                    bottom:0;
                    left:0;
                    right:0;
                    text-align:center;
                    font-size:13px;
                    font-weight:600;
                    border-top:1px solid #000;
                    padding-top:8px;
                    padding-bottom:4px;
                    background:#fff;
                }

            </style>

        </head>

        <body>

            <div style="
                width:700px;
                margin:0 auto 20px auto;
                display:flex;
                align-items:center;
                justify-content:center;
                gap:10px;
            ">

                <img src="/images/logo2.jpg"
                     style="width:65px; height:auto;" />

                <div style="text-align:right; line-height:1.3;">

                    <div style="font-size:18px; font-weight:700;">
                        مصنع السلطان للأكواب الورقية
                    </div>

                    <div style="font-size:12px; color:#444;">
                        أبناء السيد
                    </div>

                </div>

            </div>

            ${content}

            <div class="print-footer">
                صناعة نفخر بها
            </div>

        </body>

        </html>
    `);

    win.document.close();
    win.focus();

    setTimeout(() => {
        win.print();
        win.close();
    }, 700);
};



//
window.printStatsCard = function () {

    const clone =
        document.querySelector('.stats-scrollable-content')
            .cloneNode(true);

    clone.querySelectorAll(
        '[class*="skeleton"],[class*="loading"]')
        .forEach(x => x.remove());

    const content = clone.outerHTML;

    const css = Array.from(
        document.querySelectorAll('link[rel="stylesheet"],style'))
        .map(x => x.outerHTML)
        .join('');

    const win = window.open('', '', 'width=1200,height=900');

    win.document.write(`

        <html dir="rtl">

        <head>

            ${css}

            <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800;900&display=swap"
                  rel="stylesheet">

            <style>

                @page{
                    size:A4;
                    margin:10mm;
                }

                body{
                    margin:0;
                    padding:0;
                    background:#fff;
                    font-family:'Cairo',sans-serif;
                    color:#111827;
                }

                .stats-scrollable-content{
                    filter:none !important;
                }

                .stats-top-bar{
                    margin-bottom:18px !important;
                }

                .top-actions{
                    display:none !important;
                }

                .summary-grid{
                    display:grid !important;
                    grid-template-columns:repeat(3,1fr) !important;
                    gap:12px !important;
                    margin-bottom:18px !important;
                }

                .mini-card{
                    min-height:auto !important;
                    height:auto !important;
                    padding:14px 16px !important;
                    border-radius:14px !important;
                    box-shadow:none !important;
                    break-inside:avoid;
                    page-break-inside:avoid;
                }

                .card-data .label{
                    font-size:12px !important;
                    margin-bottom:6px !important;
                }

                .card-data .value{
                    font-size:28px !important;
                    line-height:1.1 !important;
                }

                .stats-section{
                    margin-bottom:16px !important;
                    border-radius:18px !important;
                    overflow:hidden;
                    break-inside:avoid;
                    page-break-inside:avoid;
                }

                .stats-section h3{
                    padding:14px 18px !important;
                    font-size:20px !important;
                    margin:0 !important;
                }

                .top-stats-grid{
    display:flex !important;
    flex-wrap:wrap !important;
    gap:12px !important;
    padding:14px !important;
}

.top-stat-card{
    width:calc(50% - 6px) !important;
}

.stats-section{
    page-break-inside:avoid !important;
    break-inside:avoid !important;
}

.top-stats-grid,
.stats-table-wrapper,
.summary-grid{
    page-break-inside:avoid !important;
    break-inside:avoid !important;
}

                .top-stat-card{
                    min-height:auto !important;
                    height:auto !important;
                    padding:16px !important;
                    border-radius:14px !important;
                    box-shadow:none !important;
                }

                .top-stat-card h4{
                    font-size:12px !important;
                    margin-bottom:6px !important;
                }

                .top-stat-card span{
                    font-size:22px !important;
                    margin-bottom:4px !important;
                    display:block;
                }

                .top-stat-card small{
                    font-size:13px !important;
                }

                .stats-table-wrapper{
                    padding:10px !important;
                }

                .stats-table{
                    width:100%;
                    border-collapse:collapse !important;
                }

                .stats-table td,
                .stats-table th{
                    border:1px solid #d1d5db !important;
                    padding:10px !important;
                    font-size:13px !important;
                }

                .print-header{
                    width:100%;
                    text-align:center;
                    margin-bottom:18px;
                }

                .print-title{
                    font-size:24px;
                    font-weight:900;
                    margin-bottom:4px;
                }

                .print-sub{
                    font-size:13px;
                    color:#6b7280;
                }

                .print-footer{
                    margin-top:20px;
                    text-align:center;
                    font-size:12px;
                    color:#6b7280;
                    border-top:1px solid #ddd;
                    padding-top:8px;
                }

            </style>

        </head>

        <body>

            <div class="print-header">

                <div class="print-title">
                    مصنع السلطان للأكواب الورقية
                </div>

                <div class="print-sub">
                    تقرير إحصائي رسمي
                </div>

            </div>

            ${content}

            <div class="print-footer">

                تم إنشاء التقرير بتاريخ:
                ${new Date().toLocaleString()}

            </div>

        </body>

        </html>
    `);

    win.document.close();

    setTimeout(() => {

        win.print();
        win.close();

    }, 700);
};


window.printArchiveDetails = function () {

    const content =
        document.querySelector('.archive-dialog-box')
            .outerHTML;

    const css = Array.from(
        document.querySelectorAll('link[rel="stylesheet"],style'))
        .map(x => x.outerHTML)
        .join('');

    const getCardValue = (labelText) => {

        const cards =
            document.querySelectorAll('.archive-detail-card');

        for (const card of cards) {

            const label =
                card.querySelector('span')
                    ?.innerText
                    ?.trim();

            if (label === labelText) {

                return card
                    .querySelector('strong')
                    ?.innerText ?? "-";
            }
        }

        return "-";
    };

    const win =
        window.open('', '', 'width=1000,height=900');

    win.document.write(`

        <html dir="rtl">

        <head>

            ${css}

            <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800;900&display=swap"
                  rel="stylesheet">

            <style>

                @page{
                    size:A4;
                    margin:14mm;
                }

                body{
                    margin:0;
                    padding:0;
                    background:#fff;
                    font-family:'Cairo',sans-serif;
                    color:#111827;
                }

                .archive-dialog-box{
                    width:100% !important;
                    max-width:none !important;
                    margin:auto !important;
                    box-shadow:none !important;
                    border:none !important;
                    overflow:visible !important;
                }

                .emp-dialog-actions{
                    display:none !important;
                }

                .emp-dialog-top-image{
                    display:none !important;
                }

                .print-header{
                    text-align:center;
                    margin-bottom:24px;
                    border-bottom:2px solid #e5e7eb;
                    padding-bottom:14px;
                }

                .print-title{
                    font-size:28px;
                    font-weight:900;
                    margin-bottom:6px;
                }

                .print-subtitle{
                    font-size:14px;
                    color:#6b7280;
                }

                .official-text{
                    margin-top:24px;
                    margin-bottom:20px;
                    line-height:2.3;
                    font-size:15px;
                    text-align:justify;
                }

                .archive-details-grid{
                    display:grid !important;
                    grid-template-columns:repeat(2,1fr) !important;
                    gap:12px !important;
                    margin-top:18px;
                }

                .archive-detail-card{
                    border:1px solid #d1d5db;
                    border-radius:12px;
                    padding:14px;
                    background:#fff;
                    break-inside:avoid;
                    page-break-inside:avoid;
                }

                .archive-detail-card span{
                    display:block;
                    font-size:13px;
                    color:#6b7280;
                    margin-bottom:6px;
                }

                .archive-detail-card strong{
                    font-size:18px;
                    font-weight:800;
                    color:#111827;
                }

                .green-text{
                    color:#059669 !important;
                }

                .red-text{
                    color:#dc2626 !important;
                }

                .print-footer{
                    margin-top:28px;
                    border-top:1px solid #e5e7eb;
                    padding-top:10px;
                    text-align:center;
                    font-size:12px;
                    color:#6b7280;
                }

                .signature-box{
                    margin-top:45px;
                    display:flex;
                    justify-content:space-between;
                    align-items:flex-start;
                }

                .signature-item{
                    width:220px;
                    text-align:center;
                }

                .signature-line{
                    border-top:1px solid #111827;
                    margin-top:55px;
                    padding-top:8px;
                    font-size:14px;
                    font-weight:700;
                }

            </style>

        </head>

        <body>

            <div class="print-header">

                <div class="print-title">
                    مصنع السلطان للأكواب الورقية
                </div>

                <div class="print-subtitle">
                    تقرير رسمي لدورة الجرد والأرشفة
                </div>

            </div>

           <div class="official-text">

                إلى السيد / مدير مصنع السلطان للأكواب الورقية المحترم

                <br><br>

                السلام عليكم ورحمة الله وبركاته،،،

                <br><br>

                نفيدكم علمًا بأنه تم تنفيذ عملية الجرد
                والأرشفة للدورة المالية الحالية بنجاح،
                وذلك بعد مراجعة واعتماد كافة البيانات
                المالية والإدارية المسجلة داخل النظام
                حتى لحظة إغلاق الدورة الحالية.

                <br><br>

                وقد أظهرت نتائج الجرد أن إجمالي السيولة
                الفعلية داخل الخزنات بلغ:

                <strong>${getCardValue("إجمالي السيولة")}</strong>

                بينما بلغ صافي الوضع المالي الحالي:

                <strong>${getCardValue("صافي الوضع المالي الحالي")}</strong>

                <br><br>

                كما سجلت حركة التدفقات المالية خلال
                الدورة الحالية إجمالي داخل قدره:

                <strong>${getCardValue("إجمالي الداخل")}</strong>

                في حين بلغ إجمالي الخارج:

                <strong>${getCardValue("إجمالي الخارج")}</strong>

                أما إجمالي الديون القائمة حتى لحظة
                الأرشفة فقد بلغ:

                <strong>${getCardValue("إجمالي الديون")}</strong>

                <br><br>

                وفيما يتعلق بالنشاط التجاري والتشغيلي،
                فقد بلغت قيمة المبيعات المحصلة:

                <strong>${getCardValue("المبيعات المحصلة")}</strong>

                بينما بلغت قيمة المشتريات المسجلة:

                <strong>${getCardValue("إجمالي المشتريات")}</strong>

                كما سجل النظام إجمالي سلف مالية بقيمة:

                <strong>${getCardValue("إجمالي السلف")}</strong>

                <br><br>

                بينما بلغت الرواتب المستحقة غير
                المسددة:

                <strong>${getCardValue("الرواتب المستحقة")}</strong>

                كما بلغت العمولات غير المصروفة:

                <strong>${getCardValue("العمولات غير المدفوعة")}</strong>

                <br><br>

                وأظهرت الإحصائيات التشغيلية أن أفضل
                مسوق خلال الدورة الحالية هو:

                <strong>${getCardValue("أفضل مسوق")}</strong>

                بينما كان المنتج الأعلى مبيعًا هو:

                <strong>${getCardValue("الأكثر مبيعًا")}</strong>

                <br><br>

                وبناءً على ما سبق، فقد تم اعتماد هذه
                البيانات رسميًا كمرجع نهائي للدورة
                المؤرشفة الحالية، وحفظها ضمن نظام
                الأرشفة المركزي الخاص بالمصنع
                للرجوع إليها عند الحاجة.

                <br><br>

                وتفضلوا بقبول فائق الاحترام والتقدير،،،

                <br><br>

                فريق العمل — نظام الحداثة لإدارة المصانع

            </div>

            ${content}

            <div class="signature-box">

                <div class="signature-item">

                    <div class="signature-line">
                        مسؤول النظام
                    </div>

                </div>

                <div class="signature-item">

                    <div class="signature-line">
                        مدير المصنع
                    </div>

                </div>

            </div>

            <div class="print-footer">

                تم إنشاء التقرير بتاريخ:
                ${new Date().toLocaleString()}

                <br>

                مصنع السلطان للأكواب الورقية — صناعة نفخر بها

            </div>

        </body>

        </html>
    `);

    win.document.close();

    setTimeout(() => {

        win.print();
        win.close();

    }, 700);
};