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



    win.document.write(`
        <html dir="rtl">
        <head>
            ${css}
            <title></title>
            <style>
                @page{
                    size:auto;
                    margin:10mm;
                }

                body{
                    margin:0;
                    padding:0;
                    background:#ffffff;
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
                }

                .pro-dialog-actions{
                    display:none !important;
                }

                .pro-dialog-overlay{
                    background:none !important;
                }
            </style>

        </head>
        <body>

            <div class="print-head">
                مصنع السلطان للأكواب الورقية
            </div>

            ${content}

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


window.printOrderCard = function () {

    const content = document.querySelector('.sal-dialog-box').outerHTML;

    const css = Array.from(document.querySelectorAll('link[rel="stylesheet"],style'))
        .map(x => x.outerHTML)
        .join('');

    const win = window.open('', '', 'width=900,height=700');

    win.document.write(`
        <html dir="rtl">
        <head>
            ${css}
          <style>
    @page{
        margin:10mm;
    }

    body{
        background:#fff;
    }

    .sal-dialog-box{
        width:700px !important;
        margin:auto !important;
        box-shadow:none !important;
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

<div style="
    width:700px;
    margin:25px auto 0 auto;
    text-align:center;
    font-size:13px;
    border-top:1px solid #000;
    padding-top:8px;
">

    <div style="font-weight:600;">
        صناعة نفخر بها
    </div>

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