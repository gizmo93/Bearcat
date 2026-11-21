<?php defined( 'ABSPATH' ) || exit; $search_id = wp_unique_id( 'release-search-' ); ?>
<form role="search" method="get" class="search-form" action="<?php echo esc_url( home_url( '/' ) ); ?>">
    <label class="screen-reader-text" for="<?php echo esc_attr( $search_id ); ?>">Search releases</label>
    <input id="<?php echo esc_attr( $search_id ); ?>" type="search" class="search-field" placeholder="Search releases …" value="<?php echo esc_attr( get_search_query() ); ?>" name="s">
    <button type="submit" class="search-submit">Search</button>
</form>
