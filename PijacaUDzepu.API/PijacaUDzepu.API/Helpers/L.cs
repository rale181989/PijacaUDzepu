namespace PijacaUDzepu.API.Helpers;

// Poruke koje API vraća korisniku (greške i potvrde) na jeziku aplikacije. Klijent šalje izabrani jezik u
// Accept-Language (sr/en/ru), LanguageMiddleware ga postavlja za ceo zahtev. Ključ je srpska poruka, pa kod
// ostaje čitljiv: throw new InvalidOperationException(L.T("Porudžbina nije pronađena.")).
// Poruka koje nema u rečniku ostaje na srpskom (test LocalizationTests proverava da su sve L.T poruke prevedene).
public static class L
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string Lang
    {
        get => Current.Value ?? "sr";
        set => Current.Value = value;
    }

    // "en-US,en;q=0.9" -> "en"; nepodržan ili prazan jezik -> "sr"
    public static string FromHeader(string? acceptLanguage)
    {
        var first = (acceptLanguage ?? "").Split(',')[0].Split(';')[0].Trim();
        var code = first.Split('-')[0].ToLowerInvariant();
        return code is "en" or "ru" ? code : "sr";
    }

    public static bool Has(string sr) => Translations.ContainsKey(sr);

    public static string T(string sr, params object?[] args)
    {
        var template = Lang != "sr" && Translations.TryGetValue(sr, out var t) ? (Lang == "en" ? t.En : t.Ru) : sr;
        return args.Length == 0 ? template : string.Format(template, args);
    }

    public static readonly IReadOnlyDictionary<string, (string En, string Ru)> Translations = new Dictionary<string, (string, string)>
    {
        // Opšte
        ["Došlo je do greške. Pokušajte ponovo."] = ("Something went wrong. Please try again.", "Произошла ошибка. Попробуйте ещё раз."),
        ["Previše pokušaja. Sačekajte malo pa pokušajte ponovo."] = ("Too many attempts. Please wait a moment and try again.", "Слишком много попыток. Подождите немного и попробуйте снова."),
        ["Fajl je prazan."] = ("The file is empty.", "Файл пустой."),
        ["Maksimalna veličina fajla je 5MB."] = ("The maximum file size is 5 MB.", "Максимальный размер файла — 5 МБ."),
        ["Dozvoljeni formati: jpg, jpeg, png, webp."] = ("Allowed formats: jpg, jpeg, png, webp.", "Допустимые форматы: jpg, jpeg, png, webp."),
        ["Neispravan format slike."] = ("Invalid image format.", "Неверный формат изображения."),
        ["Početni datum mora biti pre krajnjeg."] = ("The start date must be before the end date.", "Начальная дата должна быть раньше конечной."),

        // Nalozi i prijava
        ["Pogrešno korisničko ime ili lozinka."] = ("Wrong username or password.", "Неверное имя пользователя или пароль."),
        ["Nalog je onemogućen. Obratite se administratoru."] = ("This account is disabled. Please contact the administrator.", "Аккаунт отключён. Обратитесь к администратору."),
        ["Previše pogrešnih pokušaja. Nalog je privremeno zaključan, pokušajte ponovo za 15 minuta."] = ("Too many wrong attempts. The account is temporarily locked, try again in 15 minutes.", "Слишком много неудачных попыток. Аккаунт временно заблокирован, попробуйте через 15 минут."),
        ["Korisničko ime je zauzeto."] = ("This username is already taken.", "Это имя пользователя уже занято."),
        ["Korisnik nije pronađen."] = ("User not found.", "Пользователь не найден."),
        ["Aktivna pozivnica za ovog prodavca već postoji."] = ("An active invitation for this vendor already exists.", "Активное приглашение для этого продавца уже существует."),
        ["Pozivnica nije validna ili je istekla."] = ("The invitation is invalid or has expired.", "Приглашение недействительно или истекло."),
        ["Pozivnica poslata."] = ("Invitation sent.", "Приглашение отправлено."),
        ["Link je istekao ili je već iskorišćen. Zatražite novi."] = ("The link has expired or has already been used. Please request a new one.", "Ссылка истекла или уже использована. Запросите новую."),
        ["Pogrešna lozinka."] = ("Wrong password.", "Неверный пароль."),
        ["Pogrešna trenutna lozinka."] = ("The current password is wrong.", "Текущий пароль неверный."),
        ["Lozinka promenjena."] = ("Password changed.", "Пароль изменён."),
        ["Ako nalog postoji i ima e-mail adresu, poslali smo link za novu lozinku."] = ("If the account exists and has an email address, we have sent a link to set a new password.", "Если аккаунт существует и у него есть e-mail, мы отправили ссылку для нового пароля."),
        ["Lozinka je promenjena. Možete se prijaviti."] = ("Your password has been changed. You can log in now.", "Пароль изменён. Теперь вы можете войти."),
        ["Nalog je obrisan."] = ("The account has been deleted.", "Аккаунт удалён."),
        ["Ovo je jedini administratorski nalog i ne može se obrisati."] = ("This is the only administrator account and cannot be deleted.", "Это единственный аккаунт администратора, его нельзя удалить."),
        ["Imate porudžbine u toku. Nalog možete obrisati kada budu završene."] = ("You have orders in progress. You can delete the account once they are finished.", "У вас есть активные заказы. Аккаунт можно удалить после их завершения."),
        ["Imate dostave u toku. Nalog možete obrisati kada ih završite."] = ("You have deliveries in progress. You can delete the account once you finish them.", "У вас есть активные доставки. Аккаунт можно удалить после их завершения."),
        ["Lozinka mora imati najmanje 8 znakova."] = ("The password must be at least 8 characters long.", "Пароль должен содержать не менее 8 символов."),
        ["Lozinka mora imati najmanje {0} znakova."] = ("The password must be at least {0} characters long.", "Пароль должен содержать не менее {0} символов."),
        ["Lozinka mora imati bar jedno malo slovo."] = ("The password must contain at least one lowercase letter.", "Пароль должен содержать хотя бы одну строчную букву."),
        ["Trenutna lozinka nije ispravna."] = ("The current password is wrong.", "Текущий пароль неверный."),
        ["Korisničko ime sme da sadrži samo slova, brojeve i znakove - . _ @ +"] = ("The username may contain only letters, digits and the characters - . _ @ +", "Имя пользователя может содержать только буквы, цифры и символы - . _ @ +"),

        // Pijace, prodavci, proizvodi
        ["Pijaca nije pronađena"] = ("Market not found.", "Рынок не найден."),
        ["Tezga nije pronađena"] = ("Stall not found.", "Прилавок не найден."),
        ["Prodavac nije pronađen."] = ("Vendor not found.", "Продавец не найден."),
        ["Niste dodeljeni prodavcu."] = ("You are not assigned to a vendor.", "Вы не привязаны к продавцу."),
        ["Kurir nije pronađen."] = ("Courier not found.", "Курьер не найден."),
        ["Proizvod nije pronađen."] = ("Product not found.", "Товар не найден."),
        ["Proizvod je već dostupan."] = ("The product is already available.", "Товар уже доступен."),
        ["Pitanje nije pronađeno."] = ("Request not found.", "Обращение не найдено."),
        ["Radno vreme nije ispravno: početak mora biti pre kraja (npr. 07:00 – 14:00)."] = ("Invalid opening hours: the start must be before the end (e.g. 07:00 – 14:00).", "Неверные часы работы: начало должно быть раньше конца (например, 07:00 – 14:00)."),

        // Radno vreme tezge (VendorAvailability.ClosedMessage)
        ["{0} trenutno ne prima porudžbine."] = ("{0} is not taking orders at the moment.", "{0} сейчас не принимает заказы."),
        ["{0} trenutno ne radi."] = ("{0} is not working at the moment.", "{0} сейчас не работает."),
        ["{0} danas ne radi – otvara se {1}."] = ("{0} is closed today – opens {1}.", "{0} сегодня не работает – открывается {1}."),
        ["{0} je sada zatvoren(a) – otvara se {1} u {2}."] = ("{0} is closed now – opens {1} at {2}.", "{0} сейчас закрыт(а) – открывается {1} в {2}."),
        ["{0} je sada zatvoren(a)."] = ("{0} is closed now.", "{0} сейчас закрыт(а)."),
        ["danas"] = ("today", "сегодня"),
        ["sutra"] = ("tomorrow", "завтра"),
        ["u ponedeljak"] = ("on Monday", "в понедельник"),
        ["u utorak"] = ("on Tuesday", "во вторник"),
        ["u sredu"] = ("on Wednesday", "в среду"),
        ["u četvrtak"] = ("on Thursday", "в четверг"),
        ["u petak"] = ("on Friday", "в пятницу"),
        ["u subotu"] = ("on Saturday", "в субботу"),
        ["u nedelju"] = ("on Sunday", "в воскресенье"),

        // Porudžbine
        ["Za dostavu kurirom unesite adresu."] = ("Enter an address for courier delivery.", "Для доставки курьером укажите адрес."),
        ["Sledeći proizvodi nisu dostupni: {0}"] = ("These products are not available: {0}", "Следующие товары недоступны: {0}"),
        ["Proizvodi sa ID {0} nisu pronađeni."] = ("Products with ID {0} were not found.", "Товары с ID {0} не найдены."),
        ["{0}: na stanju je još samo {1}."] = ("{0}: only {1} left in stock.", "{0}: в наличии осталось только {1}."),
        ["{0}: nema više na stanju."] = ("{0}: out of stock.", "{0}: больше нет в наличии."),
        ["Porudžbina nije pronađena."] = ("Order not found.", "Заказ не найден."),
        ["Upišite razlog odbijanja – kupac će ga dobiti."] = ("Enter the reason for rejecting – the customer will receive it.", "Укажите причину отказа – покупатель её получит."),
        ["Ovu porudžbinu dostavlja kurir i on je završava kada je isporuči."] = ("A courier delivers this order and completes it on delivery.", "Этот заказ доставляет курьер, он и завершает его после доставки."),
        ["Samo porudžbine na čekanju mogu biti otkazane."] = ("Only pending orders can be cancelled.", "Отменить можно только ожидающие заказы."),
        ["Otkazivanje porudžbina trenutno nije moguće. Javite se prodavcu."] = ("Orders cannot be cancelled at the moment. Please contact the vendor.", "Отмена заказов сейчас недоступна. Свяжитесь с продавцом."),
        ["Porudžbinu možete oceniti kada je preuzmete."] = ("You can rate the order once you have received it.", "Оценить заказ можно после его получения."),
        ["Porudžbina je već ocenjena."] = ("This order has already been rated.", "Этот заказ уже оценён."),
        ["Link za praćenje nije ispravan."] = ("The tracking link is not valid.", "Ссылка для отслеживания недействительна."),
        ["Link za praćenje nije ispravan ili je istekao."] = ("The tracking link is not valid or has expired.", "Ссылка для отслеживания недействительна или истекла."),
        ["Nije moguć prelaz statusa iz '{0}' u '{1}'."] = ("The status cannot change from '{0}' to '{1}'.", "Нельзя изменить статус с '{0}' на '{1}'."),

        // Kuriri
        ["Uključite smenu da biste prihvatali porudžbine."] = ("Start your shift to accept orders.", "Начните смену, чтобы принимать заказы."),
        ["Porudžbinu je već preuzeo drugi kurir ili više nije dostupna."] = ("Another courier has already taken this order or it is no longer available.", "Этот заказ уже взял другой курьер или он больше недоступен."),
        ["Ove porudžbine su već preuzete ili više nisu dostupne."] = ("These orders have already been taken or are no longer available.", "Эти заказы уже взяты или больше недоступны."),
        ["Robu možete preuzeti tek kada prodavac potvrdi porudžbinu."] = ("You can pick up the goods only after the vendor confirms the order.", "Забрать товар можно только после подтверждения заказа продавцом."),
        ["Isporuku možete potvrditi tek nakon što preuzmete robu."] = ("You can confirm delivery only after picking up the goods.", "Подтвердить доставку можно только после получения товара."),
    };
}
