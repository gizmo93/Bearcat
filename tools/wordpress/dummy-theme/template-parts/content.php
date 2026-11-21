<?php defined( 'ABSPATH' ) || exit; ?>
<article id="post-<?php the_ID(); ?>" <?php post_class( 'release-card' ); ?>>
    <?php if ( is_singular() ) : ?>
        <h1 class="entry-title"><?php the_title(); ?></h1>
    <?php else : ?>
        <h2 class="entry-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h2>
    <?php endif; ?>
    <div class="entry-meta">
        <time datetime="<?php echo esc_attr( get_the_date( DATE_W3C ) ); ?>"><?php echo esc_html( get_the_date( 'M j, Y' ) ); ?> · <?php echo esc_html( get_the_time( 'H:i' ) ); ?></time>
        <span aria-hidden="true">/</span>
        <?php the_category(); ?>
    </div>
    <div class="entry-content"><?php
        the_content( 'View release →' );
        wp_link_pages();
    ?></div>
    <?php if ( has_tag() ) : ?><div class="tag-links"><?php the_tags( '', ' ', '' ); ?></div><?php endif; ?>
</article>
