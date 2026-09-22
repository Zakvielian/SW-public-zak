# Взаимодействие с флаконом
perfume-bottle-empty = Флакон пуст!
perfume-bottle-spray-self = Вы распыляете духи на себя.

# Система запахов и осмотра
bad-smell-cannot-smell = [color=gray]Вы не чувствуете запахи[/color]
bad-smell-scents-line = Пахнет: {$scents}
smell-examined = Пахнет: [color={$color}]{$smell}[/color]

perfume-scent-weak = {$scent} (слабо)
perfume-scent-normal = {$scent}
perfume-scent-strong = {$scent} (сильно)

bad-smell-level-moderate = [color=sandybrown]Воняет[/color]
bad-smell-level-heavy = [color=orange]Невероятно смрадит[/color]
bad-smell-level-fresh = [color=green]Пахнет свежестью[/color]

# Названия ароматов для вывода в осмотре
perfume-scent-rose = розами
perfume-scent-lavender = лавандой
perfume-scent-amber = тяжёлым амбровым мускусом

# Физические описания реагентов
reagent-physical-desc-fragrant = душистая
reagent-physical-desc-calming = травянистая
reagent-physical-desc-resinous = смолистая

# Реагенты: Розовые духи
reagent-name-medieval-perfume-rose = розовые духи
reagent-desc-medieval-perfume-rose = Сладкий цветочный аромат, полученный путем перегонки лепестков дикой розы.

# Реагенты: Лавандовая вода
reagent-name-medieval-perfume-lavender = лавандовая вода
reagent-desc-medieval-perfume-lavender = Успокаивающий травяной парфюм с выраженными нотами лаванды.

# Реагенты: Амбровый мускус
reagent-name-medieval-perfume-amber = амбровый мускус
reagent-desc-medieval-perfume-amber = Тяжёлый, роскошный аристократический мускус со смолистым оттенком.

ent-PerfumeBottle = флакон
    .desc = Аппарат для разбрызгивания малого количества жидкости.
    .suffix = { "Средневековье" }

ent-PerfumeBottleRose = флакон розовых духов
    .desc = Изящный флакон с распылителем, наполненный сладкими духами из лепестков розы.
    .suffix = { "Средневековье" }

ent-PerfumeBottleLavender = флакон лавандовой воды
    .desc = Небольшой флакон с успокаивающим и свежим лавандовым ароматом.
    .suffix = { "Средневековье" }

ent-PerfumeBottleAmber = флакон амбрового мускуса
    .desc = Роскошный тяжелый флакон, содержащий редкий и стойкий аристократический мускус.
    .suffix = { "Средневековье" }


smell-profile-format = { $strength }, { $base }, { $modifier }

# Сила запаха
smell-strength-faint = слабый
smell-strength-moderate = умеренный
smell-strength-strong = сильный
smell-strength-overwhelming = резкий

# Базовый профиль
smell-base-sweet = сладкий
smell-base-sour = кислый
smell-base-salty = солёный
smell-base-bitter = горький
smell-base-rotten = гнилостный
smell-base-metallic = металлический

# Оттенок / Модификатор
smell-modifier-tart = тёрпкий
smell-modifier-musty = затхлый
smell-modifier-pungent = едкий
smell-modifier-floral = цветочный
smell-modifier-spicy = пряный
smell-modifier-chemical = алхимический

# Экшен
action-track-bad-smell-name = Искать по запаху
action-track-bad-smell-desc = Вынюхать след с предмета и определить, где находится его обладатель.

# Сообщения системы
bad-smell-track-no-scent = На этом предмете нет чужого запаха.
bad-smell-track-scent-expired = Запах на предмете уже полностью выветрился.
bad-smell-track-target-lost = След теряется, цель найти не удалось.
bad-smell-track-different-grid = Запах цели чувствуется слишком далеко или за пределами этой зоны.
bad-smell-track-success = Запах ведёт на { $dir }, расстояние примерно { $dist } м.

# Стороны света
bad-smell-dir-north = север
bad-smell-dir-south = юг
bad-smell-dir-east = восток
bad-smell-dir-west = запад
bad-smell-dir-northeast = северо-восток
bad-smell-dir-northwest = северо-запад
bad-smell-dir-southeast = юго-восток
bad-smell-dir-southwest = юго-запад
